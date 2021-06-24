using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public struct BoundingSphere
    {
        public Vector3 Center;
        public float Radius;

        public BoundingSphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public readonly override string ToString()
        {
            return string.Format("Center:{0}, Radius:{1}", Center, Radius);
        }

        public readonly bool Contains(Vector3 point)
        {
            return (Center - point).LengthSquared() <= Radius * Radius;
        }

        public static BoundingSphere CreateFromPoints(ReadOnlySpan<Vector3> points)
        {
            Vector3 center = Vector3.Zero;
            foreach (Vector3 pt in points)
            {
                center += pt;
            }

            center /= points.Length;

            float maxDistanceSquared = 0f;
            foreach (Vector3 pt in points)
            {
                float distSq = Vector3.DistanceSquared(center, pt);
                if (distSq > maxDistanceSquared)
                {
                    maxDistanceSquared = distSq;
                }
            }

            return new BoundingSphere(center, MathF.Sqrt(maxDistanceSquared));
        }
    }
}
