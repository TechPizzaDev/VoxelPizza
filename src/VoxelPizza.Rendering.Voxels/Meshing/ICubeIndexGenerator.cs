
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe interface ICubeIndexGenerator<T> : IIndexGenerator<T>
       where T : unmanaged
    {
        uint AppendTop(T* destination, ref uint vertexOffset);
        uint AppendBottom(T* destination, ref uint vertexOffset);
        uint AppendLeft(T* destination, ref uint vertexOffset);
        uint AppendRight(T* destination, ref uint vertexOffset);
        uint AppendFront(T* destination, ref uint vertexOffset);
        uint AppendBack(T* destination, ref uint vertexOffset);
    }
}
