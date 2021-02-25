namespace VoxelPizza.Client
{
    public ref struct StoredChunkMesh
    {
        public ByteStore<uint> Indices;
        public ByteStore<ChunkSpaceVertex> SpaceVertices;
        public ByteStore<ChunkPaintVertex> PaintVertices;

        public StoredChunkMesh(
            ByteStore<uint> indices,
            ByteStore<ChunkSpaceVertex> spaceVertices,
            ByteStore<ChunkPaintVertex> paintVertices)
        {
            Indices = indices;
            SpaceVertices = spaceVertices;
            PaintVertices = paintVertices;
        }

        public void Dispose()
        {
            Indices.Dispose();
            SpaceVertices.Dispose();
            PaintVertices.Dispose();
        }
    }
}
