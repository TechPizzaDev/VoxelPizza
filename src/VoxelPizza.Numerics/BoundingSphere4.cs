using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public readonly struct BoundingSphere4
    {
        private readonly Vector4 _value;

        public Vector3 Center => _value.ToVector3();

        public float Radius => _value.W;

        public BoundingSphere4(Vector4 center, float radius)
        {
            _value = center with { W = radius };
        }

        public BoundingSphere4(Vector3 center, float radius)
        {
            _value = new Vector4(center, radius);
        }

        public Vector4 ToVector4()
        {
            return _value;
        }

        public override readonly string ToString()
        {
            return $"Center:{_value.ToVector3()}, Radius:{Radius}";
        }

        public readonly bool Contains(Vector4 point)
        {
            Vector4 p = _value - point;
            float r = Radius;
            return p.ToVector3().LengthSquared() <= r * r;
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
                maxDistanceSquared = MathF.Max(maxDistanceSquared, distSq);
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
                maxDistanceSquared = MathF.Max(maxDistanceSquared, distSq);
            }

            return new BoundingSphere4(center, MathF.Sqrt(maxDistanceSquared));
        }
    }
}
