using System.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkMeshRegion
    {
        private struct StoredChunk
        {
            public ChunkPosition Position { get; }
            public ChunkPosition LocalPosition { get; }
            public bool HasValue { get; }

            public ChunkRenderInfo RenderInfo;
            public ChunkMeshResult StoredMesh;
            public int IsBuildRequired;
            public bool IsUploadRequired;

            public StoredChunk(ChunkPosition position, ChunkPosition localPosition)
            {
                Position = position;
                LocalPosition = localPosition;
                HasValue = true;

                RenderInfo = new ChunkRenderInfo
                {
                    Translation = new Vector4(
                        Position.X * Chunk.Width,
                        Position.Y * Chunk.Height,
                        Position.Z * Chunk.Depth,
                        0)
                };

                StoredMesh = default;
                IsBuildRequired = 0;
                IsUploadRequired = false;
            }
        }
    }
}
