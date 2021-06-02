namespace VoxelPizza.Client
{
    public interface IVertexGenerator<T>
        where T : unmanaged
    {
        /// <summary>
        /// The maximum amount of vertices generated per contract.
        /// </summary>
        int MaxVertices { get; }

        void AppendFirst(ref ByteStore<T> store);
        void AppendLast(ref ByteStore<T> store);
    }
}
