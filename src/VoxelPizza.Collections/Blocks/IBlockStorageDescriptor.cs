namespace VoxelPizza.Collections.Blocks
{
    public interface IBlockStorageDescriptor
    {
        static abstract int Width { get; }
        static abstract int Height { get; }
        static abstract int Depth { get; }
    }
}
