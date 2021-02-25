namespace VoxelPizza.Client
{
    public interface IVertexGenerator<T>
        where T : unmanaged
    {
        void AppendVertices(ref ByteStore<T> store);
    }
}
