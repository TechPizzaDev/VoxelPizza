using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public interface IReadableBlockStorage : IBlockStorage
    {
        uint GetBlock(int x, int y, int z);
        
        void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan);
    }
}
