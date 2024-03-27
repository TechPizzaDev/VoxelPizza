
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe interface IIndexGenerator<T>
        where T : unmanaged
    {
        /// <summary>
        /// The maximum amount of indices generated per contract.
        /// </summary>
        uint MaxIndices { get; }

        uint AppendFirst(T* destination, ref uint vertexOffset);
        uint AppendLast(T* destination, ref uint vertexOffset);
    }
}
