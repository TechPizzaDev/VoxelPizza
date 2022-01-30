using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public static class XoshiroRandomExtensions
    {
        /// <summary>
        /// Produces a random angle value.
        /// </summary>
        /// <returns>A random angle value.</returns>
        public static float NextAngle32(this ref XoshiroRandom random)
        {
            return random.NextFloat32(-MathF.PI, MathF.PI);
        }

        public static Vector2 NextUnitVector2(this ref XoshiroRandom random)
        {
            float angle = random.NextAngle32();
            (float sin, float cos) = MathF.SinCos(angle);
            return new Vector2(cos, sin);
        }

        public static Vector3 NextUnitVector3(this ref XoshiroRandom random)
        {
            float u = random.NextFloat32();
            float v = random.NextFloat32();

            float t = u * 2 * MathF.PI;
            float z = 2 * v - 1;
            float sf = MathF.Sqrt(1 - z * z);
            (float sin, float cos) = MathF.SinCos(t);
            float x = cos * sf;
            float y = sin * sf;

            return new Vector3(x, y, z);
        }
    }
}
