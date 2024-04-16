using System;

namespace VoxelPizza.Collections.Blocks
{
    public interface IWritableBlockStorage : IBlockStorage
    {
        void SetBlock(int x, int y, int z, uint value);

        void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source);

        void SetBlockLayer(int y, ReadOnlySpan<uint> source);

        void FillBlock(ReadOnlySpan<uint> source);

        void SetBlockRow(int x, int y, int z, uint value);
        
        void SetBlockLayer(int y, uint value);

        void FillBlock(uint value);
    }
}
