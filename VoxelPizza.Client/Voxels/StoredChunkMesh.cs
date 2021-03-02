using System;

namespace VoxelPizza.Client
{
    public struct StoredChunkMesh : IDisposable
    {
        public ByteStorage<uint> Indices;
        public ByteStorage<ChunkSpaceVertex> SpaceVertices;
        public ByteStorage<ChunkPaintVertex> PaintVertices;

        public StoredChunkMesh(
            ByteStorage<uint> indices,
            ByteStorage<ChunkSpaceVertex> spaceVertices,
            ByteStorage<ChunkPaintVertex> paintVertices)
        {
            Indices = indices;
            SpaceVertices = spaceVertices;
            PaintVertices = paintVertices;
        }

        public StoredChunkMesh(ChunkMeshResult result) : this(
            new(result.Indices),
            new(result.SpaceVertices),
            new(result.PaintVertices))
        {
        }

        public void Dispose()
        {
            Indices.Dispose();
            SpaceVertices.Dispose();
            PaintVertices.Dispose();
        }
    }
}
