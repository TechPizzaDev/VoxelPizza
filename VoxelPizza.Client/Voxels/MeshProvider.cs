using System.Numerics;

namespace VoxelPizza.Client
{
    public abstract class MeshProvider
    {
        public abstract void Provide(
            ref MeshState meshState,
            uint blockId,
            Vector3 position);
    }
}