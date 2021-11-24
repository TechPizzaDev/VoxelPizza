namespace VoxelPizza.Client
{
    public unsafe readonly struct CubePaintVertexGenerator : ICubeVertexGenerator<ChunkPaintVertex>
    {
        private readonly ChunkPaintVertex _vertex;

        public TextureAnimation TextureAnimation => _vertex.TexAnimation0;
        public uint TextureRegion => _vertex.TexRegion0;

        public uint MaxVertices => 4 * 6;

        public CubePaintVertexGenerator(TextureAnimation textureAnimation, uint textureRegion)
        {
            _vertex = new ChunkPaintVertex(textureAnimation, textureRegion);
        }

        public void AppendFirst(ref ByteStore<ChunkPaintVertex> store)
        {
        }

        public void AppendLast(ref ByteStore<ChunkPaintVertex> store)
        {
        }

        public void AppendBack(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }

        public void AppendBottom(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }

        public void AppendFront(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }

        public void AppendLeft(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }

        public void AppendRight(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }

        public void AppendTop(ref ByteStore<ChunkPaintVertex> store)
        {
            ChunkPaintVertex* ptr = store.GetAppendPtr(4);
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
        }
    }
}
