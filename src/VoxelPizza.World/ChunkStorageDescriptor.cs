using VoxelPizza.Collections.Blocks;

namespace VoxelPizza.World
{
    public struct ChunkStorageDescriptor : IBlockStorageDescriptor
    {
        public static int Width => Chunk.Width;
        public static int Height => Chunk.Height;
        public static int Depth => Chunk.Depth;
    }
}
