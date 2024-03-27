
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe interface IVertexGenerator<T>
        where T : unmanaged
    {
        /// <summary>
        /// The maximum amount of vertices generated per contract.
        /// </summary>
        uint MaxVertices { get; }

        uint AppendFirst(T* destination);
        uint AppendLast(T* destination);
    }
}
