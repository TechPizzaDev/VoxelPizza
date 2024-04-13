using System;

namespace VoxelPizza.Collections.Blocks
{
    public interface IWritableBlockStorage : IBlockStorage
    {
        void SetBlock(int x, int y, int z, uint value);

        void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> values);

        void SetBlockLayer(int y, ReadOnlySpan<uint> value);

        void SetBlockRow(int x, int y, int z, uint value);

        void SetBlockLayer(int y, uint value);
    }
}
