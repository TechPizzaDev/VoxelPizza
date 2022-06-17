namespace VoxelPizza.Client
{
    public interface IVertexGenerator<T>
        where T : unmanaged
    {
        /// <summary>
        /// The maximum amount of vertices generated per contract.
        /// </summary>
        uint MaxVertices { get; }

        void AppendFirst(ref ByteStore<T> store);
        void AppendLast(ref ByteStore<T> store);
    }
}
