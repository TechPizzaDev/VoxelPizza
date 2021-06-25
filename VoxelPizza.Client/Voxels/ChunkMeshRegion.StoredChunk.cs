using System;
using System.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkMeshRegion
    {
        private struct StoredChunk
        {
            public Chunk Chunk { get; }
            public ChunkPosition LocalPosition { get; }
            public ChunkInfo ChunkInfo;

            public bool IsDirty;
            public ChunkMeshResult StoredMesh;

            public bool HasValue => Chunk != null;

            public StoredChunk(Chunk chunk, ChunkPosition localPosition)
            {
                Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
                LocalPosition = localPosition;

                ChunkInfo = new ChunkInfo
                {
                    Translation = new Vector3(
                        chunk.X * Chunk.Width,
                        chunk.Y * Chunk.Height,
                        chunk.Z * Chunk.Depth)
                };

                IsDirty = false;
                StoredMesh = default;
            }
        }
    }
}
