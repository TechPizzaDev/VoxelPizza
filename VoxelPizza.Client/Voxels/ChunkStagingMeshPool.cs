using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMeshPool : GraphicsResource
    {
        private List<ChunkStagingMesh> _all = new();
        private ConcurrentStack<ChunkStagingMesh> _pool = new();

        public ChunkStagingMeshPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var mesh = new ChunkStagingMesh(1024 * 1024 * 16);
                _all.Add(mesh);
                _pool.Push(mesh);
            }
        }

        private void ThrowIsDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            foreach (ChunkStagingMesh mesh in _all)
            {
                mesh.CreateDeviceObjects(gd, cl, sc);
            }
        }

        public override void DestroyDeviceObjects()
        {
            foreach (ChunkStagingMesh mesh in _all)
            {
                mesh.DestroyDeviceObjects();
            }
        }

        public bool TryRent(
            [MaybeNullWhen(false)] out ChunkStagingMesh mesh,
            uint byteCount)
        {
            if (IsDisposed)
            {
                ThrowIsDisposed();
            }

            return _pool.TryPop(out mesh);
        }

        public void Return(ChunkStagingMesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (IsDisposed)
            {
                mesh.Dispose();
                return;
            }

            _pool.Push(mesh);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (ChunkStagingMesh mesh in _all)
                {
                    mesh.Dispose();
                }
                _all.Clear();
                _pool.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
