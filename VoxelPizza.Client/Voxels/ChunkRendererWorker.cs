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
        struct Submission
        {
            public CommandListFence CLF;
            public ChunkStagingMesh Mesh;
        }

        private ChunkMesher _mesher;
        private BlockMemory _blockMemory;
        private ChunkStagingMeshPool _stagingMeshPool;
        private CommandListFencePool _commandListFencePool;
        private string? _workerName;

        private Queue<ChunkMeshBase> _meshes;
        private List<Submission> _submissions;
        private ManualResetEventSlim _workEvent;
        private ManualResetEventSlim _exitEvent;

        private bool _submitRequested;
        private bool _executing;
        private Thread _thread;

        private GraphicsDevice _gd;

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

            foreach (Submission submission in _submissions)
            {
                _gd.WaitForFence(submission.CLF.Fence, TimeSpan.FromSeconds(10));

                if (submission.Mesh != null)
                {
                    _stagingMeshPool.Return(submission.Mesh);
                }
                _commandListFencePool.Return(submission.CLF);
            }
            _submissions.Clear();

            if (clf != null)
            {
                clf.CommandList.End();

                _commandListFencePool.Return(clf);
                clf = null;
            }
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

            if (_submitRequested)
            {
                return false;
            }

            return true;
        }

        private CommandListFence? clf;

        private void ProcessMeshQueue()
        {
            if (clf == null)
            {
                if (!_commandListFencePool.TryRent(out clf))
                {
                    return;
                }
                clf.CommandList.Begin();
            }

            while (TryDequeue(out ChunkMeshBase? mesh))
            {
                if (!Monitor.TryEnter(mesh.WorkerMutex))
                {
                    // A worker is already processing this mesh.
                    continue;
                }

                try
                {
                    _ = ProcessMesh(clf.CommandList, mesh, out ChunkStagingMesh? stagingMesh);

                    if (stagingMesh != null)
                    {
                        clf.CommandList.End();

                        clf.Fence.Reset();
                        _gd.SubmitCommands(clf.CommandList, clf.Fence);

                        _submissions.Add(new Submission { CLF = clf, Mesh = stagingMesh });

                        if (!_commandListFencePool.TryRent(out clf))
                        {
                            break;
                        }
                        clf.CommandList.Begin();
                    }
                }
                catch
                {
                    if (clf != null)
                    {
                        clf.CommandList.End();
                        _commandListFencePool.Return(clf);
                    }
                    throw;
                }
                finally
                {
                    Monitor.Exit(mesh.WorkerMutex);
                }
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
                        submission.Mesh.Owner?.UploadFinished();
                        submission.Mesh.Owner = null;

                        _stagingMeshPool.Return(submission.Mesh);
                        _commandListFencePool.Return(submission.CLF);

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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
