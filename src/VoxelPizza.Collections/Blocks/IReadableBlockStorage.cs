using System;

namespace VoxelPizza.Collections.Blocks
{
    public interface IReadableBlockStorage : IBlockStorage
    {
        uint GetBlock(int x, int y, int z);

        void GetBlockRow(int x, int y, int z, Span<uint> destination);

        void GetBlockLayer(int y, Span<uint> destination);
    }
}
