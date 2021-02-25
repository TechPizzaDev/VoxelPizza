namespace VoxelPizza.Client
{
    public interface IIndexGenerator<T>
        where T : unmanaged
    {
        void AppendIndices(ref ByteStore<T> store, ref uint vertexOffset);
    }
}
