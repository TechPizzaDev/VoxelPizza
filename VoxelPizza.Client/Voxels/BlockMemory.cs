using VoxelPizza.Numerics;

namespace VoxelPizza.Client
{
    public class BlockMemory
    {
        public uint[] Data;

        public Size3 InnerSize { get; }
        public Size3 OuterSize { get; }

        public BlockMemory(Size3 innerSize, Size3 outerSize)
        {
            InnerSize = innerSize;
            OuterSize = outerSize;

            Data = new uint[OuterSize.Volume];
        }

        public static nint GetIndexBase(nint depth, nint width, nint y, nint z)
        {
            return (y * depth + z) * width;
        }
    }
}
