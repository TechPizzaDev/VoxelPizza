namespace VoxelPizza.Client
{
    public struct CubePaintVertexGenerator : ICubeVertexGenerator<ChunkPaintVertex>
    {
        public int MaxVerticesPerBlock => 4 * 6;

        public TextureAnimation TextureAnimation { get; }
        public uint TextureRegion { get; }

        public CubePaintVertexGenerator(TextureAnimation textureAnimation, uint textureRegion)
        {
            TextureAnimation = textureAnimation;
            TextureRegion = textureRegion;
        }

        public void AppendBack(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendBottom(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendFront(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendLeft(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendRight(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendTop(ref ByteStore<ChunkPaintVertex> store)
        {
            var vertex = new ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }
    }
}
