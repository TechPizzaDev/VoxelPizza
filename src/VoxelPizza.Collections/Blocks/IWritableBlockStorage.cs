using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public interface IWritableBlockStorage : IBlockStorage
    {
        bool SetBlock(int x, int y, int z, uint value);

        uint SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan);

        uint FillBlock(Int3 offset, Size3 size, uint value);
    }
}
