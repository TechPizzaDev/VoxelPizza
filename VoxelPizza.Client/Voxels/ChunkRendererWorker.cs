using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkRendererWorker : GraphicsResource
    {
        class Submission
        {
            public CommandListFence CLF = null!;
            public List<ChunkStagingMesh> Meshes = new();
        }

        private ChunkMesher _mesher;
        private BlockMemory _blockMemory;
        private ChunkStagingMeshPool _stagingMeshPool;
        private CommandListFencePool _commandListFencePool;
        private Stack<Submission> _submissionPool;
        private string? _workerName;

        private Queue<ChunkMeshBase> _meshes;
        private List<Submission> _submissions;
        private ManualResetEventSlim _workEvent;
        private ManualResetEventSlim _exitEvent;

        private bool _executing;
        private Thread _thread;

        private GraphicsDevice _gd;
        private Submission? _currentSubmission;

        public string? WorkerName
        {
            get => _workerName;
            set
            {
                _workerName = value;
                if (_thread != null)
                    _thread.Name = _workerName;
            }
        }

        public int MaxUploadsPerCommandList { get; set; } = 4;

        public ChunkRendererWorker(
            ChunkMesher mesher,
            BlockMemory blockMemory,
            ChunkStagingMeshPool stagingMeshPool,
            CommandListFencePool commandListFencePool)
        {
            _mesher = mesher ?? throw new ArgumentNullException(nameof(mesher));
            _blockMemory = blockMemory ?? throw new ArgumentNullException(nameof(blockMemory));
            _stagingMeshPool = stagingMeshPool ?? throw new ArgumentNullException(nameof(stagingMeshPool));
            _commandListFencePool = commandListFencePool ?? throw new ArgumentNullException(nameof(commandListFencePool));

            _meshes = new Queue<ChunkMeshBase>();
            _submissions = new List<Submission>();
            _submissionPool = new Stack<Submission>();

            _workEvent = new ManualResetEventSlim();
            _exitEvent = new ManualResetEventSlim();
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _gd = gd ?? throw new ArgumentNullException(nameof(gd));

            _executing = true;
            _thread = new Thread(WorkerThread);
            _thread.Name = _workerName;
            _thread.Start();
        }

        public override void DestroyDeviceObjects()
        {
            if (_executing)
            {
                _executing = false;
                _workEvent.Set();

                _exitEvent.Wait();
                _exitEvent.Reset();
            }

            if (!_thread.Join(TimeSpan.FromSeconds(10)))
            {
                throw new Exception("Failed to terminate worker thread.");
            }

            if (_currentSubmission != null)
            {
                _currentSubmission.CLF.CommandList.End();
                ClearSubmission(_currentSubmission);
                _currentSubmission = null;
            }

            foreach (Submission submission in _submissions)
            {
                _gd.WaitForFence(submission.CLF.Fence, TimeSpan.FromSeconds(10));

                ClearSubmission(submission);
            }
            _submissions.Clear();
        }

        public void Signal<TMeshes>(TMeshes meshes)
            where TMeshes : IEnumerator<ChunkMeshBase>
        {
            lock (_meshes)
            {
                _meshes.Clear();

                while (meshes.MoveNext())
                {
                    _meshes.Enqueue(meshes.Current);
                }
            }

            _workEvent.Set();
        }

        private bool ProcessMesh(CommandList cl, ChunkMeshBase mesh, out ChunkStagingMesh? stagingMesh)
        {
            // TODO: Track result?
            _ = mesh.Build(_mesher, _blockMemory);

            if (!mesh.Upload(_gd, cl, _stagingMeshPool, out stagingMesh))
            {
                // We ran out of staging meshes.
                return false;
            }

            return true;
        }

        private void FlushSubmission(ref Submission? submission)
        {
            Debug.Assert(submission != null);

            CommandListFence clf = submission.CLF;
            clf.CommandList.End();

            clf.Fence.Reset();
            _gd.SubmitCommands(clf.CommandList, clf.Fence);

            _submissions.Add(submission);
            submission = null;
        }

        private bool TryRentSubmission(ref Submission? submission)
        {
            if (submission != null)
                return true;

            if (_commandListFencePool.TryRent(out CommandListFence? clf))
            {
                if (!_submissionPool.TryPop(out submission))
                    submission = new Submission();

                submission.CLF = clf;

                clf.CommandList.Begin();
                return true;
            }
            submission = null;
            return false;
        }

        private void ProcessMeshQueue()
        {
            ref Submission? sub = ref _currentSubmission;
            if (!TryRentSubmission(ref sub))
                return;

            while (TryDequeue(out ChunkMeshBase? mesh))
            {
                if (!Monitor.TryEnter(mesh.WorkerMutex))
                {
                    // A worker is already processing this mesh.
                    continue;
                }

                try
                {
                    Debug.Assert(sub != null);

                    bool success = ProcessMesh(sub.CLF.CommandList, mesh, out ChunkStagingMesh? stagingMesh);
                    if (!success)
                    {
                        if (sub.Meshes.Count > 0)
                            FlushSubmission(ref sub);
                        break;
                    }

                    if (stagingMesh != null)
                    {
                        sub.Meshes.Add(stagingMesh);

                        if (sub.Meshes.Count >= MaxUploadsPerCommandList)
                        {
                            FlushSubmission(ref sub);
                            if (!TryRentSubmission(ref sub))
                                return;
                        }
                    }
                }
                catch
                {
                    if (sub != null)
                    {
                        sub.CLF.CommandList.End();
                        ClearSubmission(sub);
                        sub = null;
                    }
                    throw;
                }
                finally
                {
                    Monitor.Exit(mesh.WorkerMutex);
                }
            }

            if (sub != null && sub.Meshes.Count > 0)
            {
                FlushSubmission(ref sub);
            }

            if (_meshes.Count == 0)
            {
                _workEvent.Reset();
            }
        }

        private bool TryDequeue([MaybeNullWhen(false)] out ChunkMeshBase mesh)
        {
            lock (_meshes)
            {
                return _meshes.TryDequeue(out mesh);
            }
        }

        private void WorkerThread()
        {
            do
            {
                _workEvent.Wait();

                bool sleep = true;

                for (int i = _submissions.Count; i-- > 0;)
                {
                    if (!_executing)
                    {
                        break;
                    }

                    Submission submission = _submissions[i];
                    if (submission.CLF.Fence.Signaled)
                    {
                        ClearSubmission(submission);

                        _submissions.RemoveAt(i);
                        sleep = false;
                    }
                }

                if (sleep)
                    Thread.Sleep(1);

                ProcessMeshQueue();
            }
            while (_executing);

            _exitEvent.Set();
        }

        private void ClearSubmission(Submission submission)
        {
            Debug.Assert(submission != null);

            foreach (ChunkStagingMesh mesh in submission.Meshes)
            {
                mesh.Owner?.UploadFinished();
                mesh.Owner = null;
                _stagingMeshPool.Return(mesh);
            }
            submission.Meshes.Clear();

            _commandListFencePool.Return(submission.CLF);

            submission.CLF = null!;
            _submissionPool.Push(submission);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
