using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMeshPool : IDisposable
    {
        private ConcurrentStack<ChunkStagingMesh> _pool;

        public ResourceFactory Factory { get; }
        public uint MaxChunksPerMesh { get; }

        public bool IsDisposed { get; private set; }

        public ChunkStagingMeshPool(ResourceFactory factory, uint maxChunksPerMesh)
        {
            if (maxChunksPerMesh < 0)
                throw new ArgumentOutOfRangeException(nameof(maxChunksPerMesh));

            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            MaxChunksPerMesh = maxChunksPerMesh;

            _pool = new();

            for (int i = 0; i < 4; i++)
            {
                var mesh = new ChunkStagingMesh(Factory, MaxChunksPerMesh);
                _pool.Push(mesh);
            }
        }

        private void ThrowIsDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        public bool TryRent([MaybeNullWhen(false)] out ChunkStagingMesh mesh, int chunkCount)
        {
            if (IsDisposed)
                ThrowIsDisposed();

            return _pool.TryPop(out mesh);
        }

        public void Return(ChunkStagingMesh mesh)
        {
            if (IsDisposed)
            {
                mesh.Dispose();
                return;
            }
            _pool.Push(mesh);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    while (_pool.TryPop(out ChunkStagingMesh? chunkMesh))
                    {
                        chunkMesh.Dispose();
                    }
                }

                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
