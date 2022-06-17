using System.Numerics;

namespace VoxelPizza
{
    public static class Vector4Extensions
    {
        public static Vector3 ToVector3(this Vector4 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }
    }
}
