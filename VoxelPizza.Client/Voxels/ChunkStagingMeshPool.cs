using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMeshPool : IDisposable
    {
        private HashSet<ChunkStagingMesh> _all = new();
        private ConcurrentStack<ChunkStagingMesh> _pool = new();

        public ResourceFactory Factory { get; }
        public uint MaxChunksPerMesh { get; }

        public bool IsDisposed { get; private set; }

        public ChunkStagingMeshPool(ResourceFactory factory, uint maxChunksPerMesh, int count)
        {
            if (maxChunksPerMesh < 0)
                throw new ArgumentOutOfRangeException(nameof(maxChunksPerMesh));

            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            MaxChunksPerMesh = maxChunksPerMesh;

            for (int i = 0; i < count; i++)
            {
                var mesh = new ChunkStagingMesh(Factory, maxChunksPerMesh, 1024 * 1024 * 16);
                _all.Add(mesh);
                _pool.Push(mesh);
            }
        }

        private void ThrowIsDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        public bool TryRent(
            [MaybeNullWhen(false)] out ChunkStagingMesh mesh,
            uint byteCount)
        {
            if (IsDisposed)
                ThrowIsDisposed();

            //mesh = new ChunkStagingMesh(Factory, MaxChunksPerMesh, byteCount);
            //_all.Add(mesh);
            //return true;
            return _pool.TryPop(out mesh);
        }

        public void Return(ChunkStagingMesh mesh)
        {
            if (IsDisposed)
            {
                mesh.Dispose();
                _all.Remove(mesh);
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
                    foreach (ChunkStagingMesh? mesh in _all)
                    {
                        mesh.Dispose();
                    }
                    _pool.Clear();
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
