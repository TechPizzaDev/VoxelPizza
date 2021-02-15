using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public abstract class CullRenderable : Renderable
    {
        public abstract BoundingBox BoundingBox { get; }

        public bool Cull(ref BoundingFrustum visibleFrustum)
        {
            return visibleFrustum.Contains(BoundingBox) == ContainmentType.Disjoint;
        }
    }
}
