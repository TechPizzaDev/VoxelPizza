
namespace VoxelPizza.Collections
{
    public interface IBlockStorage
    {
        void GetBlockRow(nint index, ref uint destination, uint length);

        void SetBlockLayer(nint y, uint value);

        void SetBlock(nint index, uint value);
    }
}
