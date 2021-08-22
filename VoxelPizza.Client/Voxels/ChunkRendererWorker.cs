using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkRendererWorker : GraphicsResource
    {
        private ChunkMesher _mesher;
        private BlockMemory _blockMemory;
        private ChunkStagingMeshPool _stagingMeshPool;
        private string? _workerName;

        private Queue<ChunkMeshBase> _meshes;
        private List<ChunkStagingMesh> _submittedStagingMeshes;
        private ManualResetEventSlim _workEvent;
        private ManualResetEventSlim _exitEvent;

        private bool _submitRequested;
        private bool _wait;
        private bool _executing;
        private Thread _thread;

        private GraphicsDevice _gd;
        private CommandList _uploadList;
        private Fence _uploadFence;

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

        public ChunkRendererWorker(ChunkMesher mesher, BlockMemory blockMemory, ChunkStagingMeshPool stagingMeshPool)
        {
            _mesher = mesher ?? throw new ArgumentNullException(nameof(mesher));
            _blockMemory = blockMemory ?? throw new ArgumentNullException(nameof(blockMemory));
            _stagingMeshPool = stagingMeshPool ?? throw new ArgumentNullException(nameof(stagingMeshPool));

            _meshes = new Queue<ChunkMeshBase>();
            _submittedStagingMeshes = new List<ChunkStagingMesh>();
            _workEvent = new ManualResetEventSlim();
            _exitEvent = new ManualResetEventSlim();
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _gd = gd ?? throw new ArgumentNullException(nameof(gd));

            _uploadList = _gd.ResourceFactory.CreateCommandList();
            _uploadFence = _gd.ResourceFactory.CreateFence(false);

            _wait = false;
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
            
            //_wait = true;

            _uploadList?.Dispose();
            _uploadList = null!;

            _uploadFence?.Dispose();
            _uploadFence = null!;

            foreach (ChunkStagingMesh? mesh in _submittedStagingMeshes)
            {
                _stagingMeshPool.Return(mesh);
            }
            _submittedStagingMeshes.Clear();
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

        private bool ProcessMesh(ChunkMeshBase mesh)
        {
            // TODO: Track result?
            _ = mesh.Build(_mesher, _blockMemory);

            if (mesh.Upload(_gd, _uploadList, _stagingMeshPool, out ChunkStagingMesh? stagingMesh))
            {
                if (stagingMesh != null)
                {
                    _submittedStagingMeshes.Add(stagingMesh);
                }
            }
            else
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

        private void ProcessMeshQueue()
        {
            // TODO: WaitForFence?
            if (!_wait)
            {
                _uploadList.Begin();

                while (TryDequeue(out ChunkMeshBase? mesh))
                {
                    if (!Monitor.TryEnter(mesh.WorkerMutex))
                    {
                        // A worker is already processing this mesh.
                        continue;
                    }

                    try
                    {
                        if (!ProcessMesh(mesh))
                        {
                            break;
                        }

                        if (!_executing)
                        {
                            return;
                        }
                    }
                    finally
                    {
                        Monitor.Exit(mesh.WorkerMutex);
                    }
                }

                _uploadList.End();

                _gd.SubmitCommands(_uploadList, _uploadFence);
                _wait = true;
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
                if (!_executing)
                {
                    break;
                }

                if (_uploadFence.Signaled)
                {
                    foreach (ChunkStagingMesh mesh in _submittedStagingMeshes)
                    {
                        mesh.Owner?.UploadFinished();
                        mesh.Owner = null;

                        _stagingMeshPool.Return(mesh);
                    }
                    _submittedStagingMeshes.Clear();

                    _uploadFence.Reset();
                    _wait = false;
                }
                else
                {
                    Thread.Sleep(1);
                }

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
