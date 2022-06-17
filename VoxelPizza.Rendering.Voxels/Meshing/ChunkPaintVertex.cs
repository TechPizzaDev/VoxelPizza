namespace VoxelPizza.Client
{
    public struct ChunkPaintVertex
    {
        public TextureAnimation TexAnimation0;
        public uint TexRegion0;

        public ChunkPaintVertex(TextureAnimation texAnimation0, uint texRegion0)
        {
            TexAnimation0 = texAnimation0;
            TexRegion0 = texRegion0;
        }
    }
}
