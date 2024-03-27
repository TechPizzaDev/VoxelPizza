
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe interface ICubeVertexGenerator<T> : IVertexGenerator<T>
        where T : unmanaged
    {
        uint AppendTop(T* destination);
        uint AppendBottom(T* destination);
        uint AppendLeft(T* destination);
        uint AppendRight(T* destination);
        uint AppendFront(T* destination);
        uint AppendBack(T* destination);
    }
}
