using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMeshPool
    {
        private ConcurrentStack<ChunkStagingMesh> _pool;

        public ResourceFactory Factory { get; }
        public int MaxChunksPerMesh { get; }

        public ChunkStagingMeshPool(ResourceFactory factory, int maxChunksPerMesh)
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

        public bool TryRent([MaybeNullWhen(false)] out ChunkStagingMesh mesh, int chunkCount)
        {
            return _pool.TryPop(out mesh);
        }

        public void Return(ChunkStagingMesh mesh)
        {
            _pool.Push(mesh);
        }
    }
}
