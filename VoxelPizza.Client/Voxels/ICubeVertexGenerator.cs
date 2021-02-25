namespace VoxelPizza.Client
{
    public interface ICubeVertexGenerator<T>
        where T : unmanaged
    {
        int MaxVerticesPerBlock { get; }

        void AppendTop(ref ByteStore<T> store);
        void AppendBottom(ref ByteStore<T> store);
        void AppendLeft(ref ByteStore<T> store);
        void AppendRight(ref ByteStore<T> store);
        void AppendFront(ref ByteStore<T> store);
        void AppendBack(ref ByteStore<T> store);
    }
}
