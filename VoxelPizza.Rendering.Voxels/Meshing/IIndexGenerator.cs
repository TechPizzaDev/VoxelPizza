namespace VoxelPizza.Client
{
    public interface IIndexGenerator<T>
        where T : unmanaged
    {
        /// <summary>
        /// The maximum amount of indices generated per contract.
        /// </summary>
        uint MaxIndices { get; }

        void AppendFirst(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendLast(ref ByteStore<T> store, ref uint vertexOffset);
    }
}
