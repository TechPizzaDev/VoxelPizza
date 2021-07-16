
namespace VoxelPizza.Collections
{
    public interface IRefEnumerator<T>
    {
        ref T Current { get; }

        bool MoveNext();
    }
}
