using System.Numerics;

namespace VoxelPizza.Numerics
{
    public readonly struct Ray
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public static Ray FromStart(Vector3 start, Vector3 end)
        {
            Vector3 direction = Vector3.Normalize(end - start);
            return new Ray(start, direction);
        }

        public VoxelRayCast CastVoxelRay()
        {
            return new VoxelRayCast(Origin, Direction);
        }
    }
}
