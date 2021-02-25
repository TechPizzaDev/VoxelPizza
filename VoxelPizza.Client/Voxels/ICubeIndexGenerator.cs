namespace VoxelPizza.Client
{
    public interface ICubeIndexGenerator<T>
       where T : unmanaged
    {
        int MaxIndicesPerBlock { get; }

        void AppendTop(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendBottom(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendLeft(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendRight(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendFront(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendBack(ref ByteStore<T> store, ref uint vertexOffset);
    }
}
