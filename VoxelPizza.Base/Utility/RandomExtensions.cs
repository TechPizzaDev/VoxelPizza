using System;
using System.Numerics;

namespace VoxelPizza
{
    public static class RandomExtensions
    {
        public static float NextSingle(this Random random, float min, float max)
        {
            return (max - min) * NextSingle(random) + min;
        }

        public static float NextSingle(this Random random, float max)
        {
            return max * NextSingle(random);
        }

        public static float NextSingle(this Random random)
        {
            return (float)random.NextDouble();
        }

        public static float NextAngle(this Random random)
        {
            return NextSingle(random, -MathF.PI, MathF.PI);
        }

        public static Vector2 NextUnitVector2(this Random random)
        {
            float angle = random.NextAngle();
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        public static Vector3 NextUnitVector3(this Random random)
        {
            float u = random.NextSingle();
            float v = random.NextSingle();

            float t = u * 2 * MathF.PI;
            float z = 2 * v - 1;
            float sf = MathF.Sqrt(1 - z * z);
            float x = sf * MathF.Cos(t);
            float y = sf * MathF.Sin(t);

            return new Vector3(x, y, z);
        }
    }
}
