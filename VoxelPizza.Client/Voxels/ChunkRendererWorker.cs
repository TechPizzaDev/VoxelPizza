using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Veldrid;
using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkRendererWorker : GraphicsResource
    {
        private class Submission
        {
            public CommandListFence CLF = null!;
            public readonly List<ChunkStagingMesh> StagingMeshes = new();
        }

        private ChunkMesher _mesher;
        private BlockMemory _blockBuffer;
        private ChunkStagingMeshPool _stagingMeshPool;
        private CommandListFencePool _commandListFencePool;
        private Stack<Submission> _submissionPool;
        private string? _workerName;

        private Queue<ChunkMeshRegion> _regionsToBuild;
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

        public int MaxUploadsPerCommandList { get; set; } = 8;

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

                FinishSubmission(submission, discard: true);
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

        public void EnqueueReset(ChunkMeshRegion region)
        {
            lock (_regionsToReset)
            {
                _regionsToReset.Enqueue(region);
            }

            _workEvent.Set();
        }

        /// <summary>
        /// </summary>
        /// <param name="gd"></param>
        /// <param name="stagingMeshPool"></param>
        /// <param name="commandList"></param>
        /// <param name="region"></param>
        /// <param name="stagingMesh"></param>
        /// <returns>Whether the upload failed due to exhausting the staging mesh pool.</returns>
        public static unsafe bool UploadRegion(
            GraphicsDevice gd,
            ChunkStagingMeshPool stagingMeshPool,
            CommandList commandList,
            ChunkMeshRegion region,
            out ChunkStagingMesh? stagingMesh)
        {
            ChunkMeshSizes sizes = region.GetMeshSizes();
            uint totalBytesRequired = sizes.TotalBytesRequired;
            if (totalBytesRequired == 0)
            {
                stagingMesh = default;
                return false;
            }

            if (!stagingMeshPool.TryRent(out stagingMesh, totalBytesRequired))
            {
                // We ran out of staging meshes.
                return true;
            }

            MappedResource bufferMap = gd.Map(stagingMesh.Buffer, 0, totalBytesRequired, MapMode.Write, 0);
            try
            {
                region.WriteMeshes(sizes, (byte*)bufferMap.Data);
            }
            finally
            {
                gd.Unmap(stagingMesh.Buffer);
            }

            ChunkMeshBuffers result = region.Copy(
                gd, commandList, sizes, stagingMesh.Buffer, 0);

            stagingMesh.Owner = region;
            stagingMesh.MeshBuffers = result;
            return false;
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
                FinishSubmission(submission, false);
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
                }

                region.Renderer.RecycleMeshRegion(region);
            }
        }

        private void ProcessMeshQueue()
        {
            ProcessResetQueue();

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

                    (bool regionBuilt, bool regionEmpty) = region.Build(_mesher, _blockBuffer);
                    if (regionBuilt)
                    {
                        if (regionEmpty)
                        {
                            region.UploadStaged();
                            region.UploadFinished(null);
                            continue;
                        }

                        region.UploadRequired();
                    }

                    if (!region.IsUploadRequired)
                    {
                        continue;
                    }

                    bool stagingPoolEmpty = UploadRegion(
                         _gd, _stagingMeshPool, submission.CLF.CommandList, region, out ChunkStagingMesh? stagingMesh);

                    if (stagingPoolEmpty)
                    {
                        Debug.Assert(stagingMesh == null);
                        break;
                    }

                    region.UploadStaged();

                    if (stagingMesh != null)
                    {
                        submission.StagingMeshes.Add(stagingMesh);
                        if (submission.StagingMeshes.Count >= MaxUploadsPerCommandList)
                        {
                            FlushSubmission(submission);

                            if (!TryRentSubmission(out submission))
                                break;
                        }
                    }
                    else
                    {
                        region.UploadFinished(null);
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
                        FinishSubmission(submission, discard: false);

                        _submissions.RemoveAt(i);
                        sleep = false;
                    }
                }

                if (sleep)
                    Thread.Sleep(1);

                ProcessMeshQueue();
            }
            while (_executing);

            ProcessResetQueue();

            _exitEvent.Set();
        }

        private void FinishSubmission(Submission submission, bool discard)
        {
            foreach (ChunkStagingMesh stagingMesh in submission.StagingMeshes)
            {
                ChunkMeshRegion? owner = stagingMesh.Owner;
                ChunkMeshBuffers? meshBuffers = stagingMesh.MeshBuffers;

                if (discard)
                {
                    meshBuffers?.Dispose();
                }
                else
                {
                    Debug.Assert(owner != null);
                    owner.UploadFinished(meshBuffers);
                }

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
