namespace VoxelPizza.Client
{
    public class Chunk
    {
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 16;

        public uint[,,] Blocks;

        public Chunk()
        {
            Blocks = new uint[ChunkHeight, ChunkWidth, ChunkWidth];
        }
    }
}
