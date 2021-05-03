using System.Numerics;

namespace VoxelPizza.Client
{
    public abstract class CullableMeshProvider : MeshProvider
    {
        public override void Provide(
            ref MeshState meshState,
            uint blockId,
            Vector3 position)
        {
            Provide(ref meshState, blockId, position, CubeFaces.All);
        }

        public virtual bool IsEmpty(CubeFaces faces)
        {
            return faces == CubeFaces.None;
        }

        public abstract void Provide(
            ref MeshState meshState,
            uint blockId,
            Vector3 position,
            CubeFaces faces);
    }
}