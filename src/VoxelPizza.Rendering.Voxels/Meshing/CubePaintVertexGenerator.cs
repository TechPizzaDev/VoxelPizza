
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public readonly unsafe struct CubePaintVertexGenerator : ICubeVertexGenerator<ChunkPaintVertex>
    {
        private const int VerticesPerFace = 4;

        private readonly ChunkPaintVertex _vertex;

        public TextureAnimation TextureAnimation => _vertex.TexAnimation0;
        public uint TextureRegion => _vertex.TexRegion0;

        public uint MaxVertices => VerticesPerFace * 6;

        public CubePaintVertexGenerator(TextureAnimation textureAnimation, uint textureRegion)
        {
            _vertex = new ChunkPaintVertex(textureAnimation, textureRegion);
        }

        public uint AppendFirst(ChunkPaintVertex* destination)
        {
            return 0;
        }

        public uint AppendLast(ChunkPaintVertex* destination)
        {
            return 0;
        }

        public uint AppendBack(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }

        public uint AppendBottom(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }

        public uint AppendFront(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }

        public uint AppendLeft(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }

        public uint AppendRight(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }

        public uint AppendTop(ChunkPaintVertex* destination)
        {
            ChunkPaintVertex* ptr = destination;
            ChunkPaintVertex vertex = _vertex;
            ptr[0] = vertex;
            ptr[1] = vertex;
            ptr[2] = vertex;
            ptr[3] = vertex;
            return VerticesPerFace;
        }
    }
}
