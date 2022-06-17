using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public struct BoundingSphere4
    {
        public Vector4 Center;
        public float Radius;

        public BoundingSphere4(Vector4 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public BoundingSphere4(Vector3 center, float radius)
        {
            Center = new Vector4(center, 0);
            Radius = radius;
        }

        public override readonly string ToString()
        {
            return string.Format("Center:{0}, Radius:{1}", Center, Radius);
        }

        public readonly bool Contains(Vector4 point)
        {
            return (Center - point).LengthSquared() <= Radius * Radius;
        }

        public static BoundingSphere4 CreateFromPoints(ReadOnlySpan<Vector3> points)
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

            return new BoundingSphere4(center, MathF.Sqrt(maxDistanceSquared));
        }

        public static BoundingSphere4 CreateFromPoints(ReadOnlySpan<Vector4> points)
        {
            Vector4 center = Vector4.Zero;
            foreach (Vector4 pt in points)
            {
                center += pt;
            }

            center /= points.Length;

            float maxDistanceSquared = 0f;
            foreach (Vector4 pt in points)
            {
                float distSq = Vector4.DistanceSquared(center, pt);
                if (distSq > maxDistanceSquared)
                {
                    maxDistanceSquared = distSq;
                }
            }

            return new BoundingSphere4(center, MathF.Sqrt(maxDistanceSquared));
        }
    }
}
