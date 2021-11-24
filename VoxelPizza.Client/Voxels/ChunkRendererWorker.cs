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
            public bool IsRecording;
            public CommandListFence CLF = null!;
            public List<ChunkStagingMesh> StagingMeshes = new();
        }

        private ChunkMesher _mesher;
        private BlockMemory _blockBuffer;
        private ChunkStagingMeshPool _stagingMeshPool;
        private CommandListFencePool _commandListFencePool;
        private Stack<Submission> _submissionPool;
        private string? _workerName;

        private Queue<ChunkMeshRegion> _regionsToBuild;
        private Queue<ChunkMeshRegion> _regionsToCleanup;
        private Queue<ChunkMeshRegion> _regionsToReset;
        private List<Submission> _submissions;
        private ManualResetEvent _workEvent;
        private ManualResetEvent _exitEvent;

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

        public int MaxUploadsPerCommandList { get; set; } = 4;

        public ChunkRendererWorker(
            ChunkMesher mesher,
            BlockMemory blockMemory,
            ChunkStagingMeshPool stagingMeshPool,
            CommandListFencePool commandListFencePool)
        {
            _mesher = mesher ?? throw new ArgumentNullException(nameof(mesher));
            _blockBuffer = blockMemory ?? throw new ArgumentNullException(nameof(blockMemory));
            _stagingMeshPool = stagingMeshPool ?? throw new ArgumentNullException(nameof(stagingMeshPool));
            _commandListFencePool = commandListFencePool ?? throw new ArgumentNullException(nameof(commandListFencePool));

            _regionsToBuild = new Queue<ChunkMeshRegion>();
            _regionsToCleanup = new Queue<ChunkMeshRegion>();
            _regionsToReset = new Queue<ChunkMeshRegion>();

            _submissions = new List<Submission>();
            _submissionPool = new Stack<Submission>();

            _workEvent = new ManualResetEvent(false);
            _exitEvent = new ManualResetEvent(false);
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

                _exitEvent.WaitOne();
                _exitEvent.Reset();
            }

            if (!_thread.Join(TimeSpan.FromSeconds(10)))
            {
                throw new Exception("Failed to terminate worker thread.");
            }

            foreach (Submission submission in _submissions)
            {
                _gd.WaitForFence(submission.CLF.Fence, TimeSpan.FromSeconds(10));

                FinishSubmission(submission);
            }
            _submissions.Clear();
        }

        public void SignalToBuild<TMeshes>(TMeshes regionsToBuild)
            where TMeshes : IEnumerator<ChunkMeshRegion>
        {
            lock (_regionsToBuild)
            {
                _regionsToBuild.Clear();

                while (regionsToBuild.MoveNext())
                {
                    _regionsToBuild.Enqueue(regionsToBuild.Current);
                }
            }

            _workEvent.Set();
        }

        public void SignalToCleanup<TMeshes>(TMeshes regionsToCleanup)
            where TMeshes : IEnumerator<ChunkMeshRegion>
        {
            lock (_regionsToCleanup)
            {
                _regionsToCleanup.Clear();

                while (regionsToCleanup.MoveNext())
                {
                    _regionsToCleanup.Enqueue(regionsToCleanup.Current);
                }
            }

            _workEvent.Set();
        }

        public void EnqueueReset(ChunkMeshRegion region)
        {
            lock (_regionsToReset)
            {
                _regionsToReset.Enqueue(region);
            }

            _workEvent.Set();
        }

        private unsafe (bool PoolEmpty, bool Empty) ProcessRegion(
            Submission submission,
            ChunkMeshRegion region,
            out ChunkStagingMesh? stagingMesh)
        {
            // TODO: Track results?
            //_ = region.Cleanup();

            if (region.IsDisposed)
            {
                stagingMesh = default;
                return (false, true);
            }

            var (built, empty) = region.Build(_mesher, _blockBuffer);
            if (!built)
            {
                stagingMesh = default;
                return (false, empty);
            }

            ChunkMeshSizes sizes = region.GetMeshSizes();
            uint totalBytesRequired = sizes.TotalBytesRequired;
            if (totalBytesRequired == 0)
            {
                stagingMesh = default;
                return (false, true);
            }

            //if (result.IsEmpty)
            //{
            //    lock (_uploadMutex)
            //    {
            //        EmptyPendingUploads();
            //
            //        // Enqueue an empty mesh to clear the current mesh.
            //        _meshesForUpload.Enqueue(null);
            //
            //        _uploadRequired = false;
            //        mesh = default;
            //        return true;
            //    }
            //}

            if (!_stagingMeshPool.TryRent(out stagingMesh, totalBytesRequired))
            {
                // We ran out of staging meshes.
                return (true, false);
            }

            try
            {
                //mapWatch.Start();
                MappedResource bufferMap = _gd.Map(stagingMesh.Buffer, 0, totalBytesRequired, MapMode.Write, 0);
                //mapWatch.Stop();
                //Console.WritefLine(mapWatch.Elapsed.TotalMilliseconds.ToString("0.0"));

                region.WriteMeshes(sizes, (byte*)bufferMap.Data);
            }
            finally
            {
                _gd.Unmap(stagingMesh.Buffer);
            }

            ChunkMeshBuffers result = region.Copy(
                _gd, submission.CLF.CommandList, sizes, stagingMesh.Buffer, 0);

            stagingMesh.Owner = region;
            stagingMesh.MeshBuffers = result;

            return (false, false);
        }

        private void FlushSubmission(Submission submission)
        {
            CommandListFence clf = submission.CLF;
            clf.CommandList.End();
            clf.Fence.Reset();

            if (submission.StagingMeshes.Count > 0)
            {
                _gd.SubmitCommands(clf.CommandList, clf.Fence);
                _submissions.Add(submission);
            }
            else
            {
                FinishSubmission(submission);
            }
        }

        private bool TryRentSubmission([MaybeNullWhen(false)] out Submission? submission)
        {
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

        private void ProcessResetQueue()
        {
            while (TryDequeueToReset(out ChunkMeshRegion? region))
            {
                lock (region.WorkerMutex)
                {
                    region.Reset();
                    region.Renderer._regionPool.Push(region);
                }
            }
        }

        private void ProcessMeshQueue()
        {
            ProcessResetQueue();

            while (TryDequeueToCleanup(out ChunkMeshRegion? region))
            {
                if (!Monitor.TryEnter(region.WorkerMutex))
                {
                    // A worker is already processing this region.
                    continue;
                }

                try
                {
                    region.Cleanup();
                }
                finally
                {
                    Monitor.Exit(region.WorkerMutex);
                }
            }

            if (!TryRentSubmission(out Submission? submission))
                return;

            while (TryDequeueToBuild(out ChunkMeshRegion? region))
            {
                if (!Monitor.TryEnter(region.WorkerMutex))
                {
                    // A worker is already processing this region.
                    continue;
                }

                try
                {
                    Debug.Assert(submission != null);

                    var (poolEmpty, empty) = ProcessRegion(submission, region, out ChunkStagingMesh? stagingMesh);
                    if (empty)
                    {
                        //region.UploadFinished(null);
                        Debug.Assert(stagingMesh == null);
                    }

                    if (poolEmpty)
                    {
                        break;
                    }

                    if (stagingMesh != null)
                    {
                        submission.StagingMeshes.Add(stagingMesh);

                        if (submission.StagingMeshes.Count >= MaxUploadsPerCommandList)
                        {
                            FlushSubmission(submission);
                            if (!TryRentSubmission(out submission))
                                return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);

                    if (submission != null)
                        FlushSubmission(submission);

                    throw;
                }
                finally
                {
                    Monitor.Exit(region.WorkerMutex);
                }
            }

            if (submission != null)
                FlushSubmission(submission);

            if (_regionsToBuild.Count == 0)
            {
                _workEvent.Reset();
            }
        }

        private bool TryDequeueToBuild([MaybeNullWhen(false)] out ChunkMeshRegion region)
        {
            lock (_regionsToBuild)
            {
                return _regionsToBuild.TryDequeue(out region);
            }
        }

        private bool TryDequeueToCleanup([MaybeNullWhen(false)] out ChunkMeshRegion region)
        {
            lock (_regionsToCleanup)
            {
                return _regionsToCleanup.TryDequeue(out region);
            }
        }

        private bool TryDequeueToReset([MaybeNullWhen(false)] out ChunkMeshRegion region)
        {
            lock (_regionsToReset)
            {
                return _regionsToReset.TryDequeue(out region);
            }
        }

        private void WorkerThread()
        {
            do
            {
                _workEvent.WaitOne();

                bool sleep = true;

                for (int i = _submissions.Count; i-- > 0 && _executing;)
                {
                    Submission submission = _submissions[i];
                    if (submission.CLF.Fence.Signaled)
                    {
                        FinishSubmission(submission);

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

        private void FinishSubmission(Submission submission)
        {
            foreach (ChunkStagingMesh stagingMesh in submission.StagingMeshes)
            {
                ChunkMeshRegion? owner = stagingMesh.Owner;
                ChunkMeshBuffers? meshBuffers = stagingMesh.MeshBuffers;

                if (owner == null)
                {
                    meshBuffers?.Dispose();
                }
                else
                {
                    Debug.Assert(meshBuffers != null);
                    owner.UploadFinished(meshBuffers);
                }

                stagingMesh.Owner = null;
                _stagingMeshPool.Return(stagingMesh);
            }
            submission.StagingMeshes.Clear();

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
