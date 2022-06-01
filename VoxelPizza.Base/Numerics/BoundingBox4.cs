using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Numerics
{
    public struct BoundingBox4 : IEquatable<BoundingBox4>
    {
        public Vector4 Min;
        public Vector4 Max;

        public BoundingBox4(Vector4 min, Vector4 max)
        {
            Min = min;
            Max = max;
        }

        public BoundingBox4(Vector3 min, Vector3 max)
        {
            Min = new Vector4(min, 0);
            Max = new Vector4(max, 0);
        }

        public readonly ContainmentType Contains(BoundingBox4 other)
        {
            if (Max.X < other.Min.X || Min.X > other.Max.X
                || Max.Y < other.Min.Y || Min.Y > other.Max.Y
                || Max.Z < other.Min.Z || Min.Z > other.Max.Z)
            {
                return ContainmentType.Disjoint;
            }
            else if (Min.X <= other.Min.X && Max.X >= other.Max.X
                && Min.Y <= other.Min.Y && Max.Y >= other.Max.Y
                && Min.Z <= other.Min.Z && Max.Z >= other.Max.Z)
            {
                return ContainmentType.Contains;
            }
            else
            {
                return ContainmentType.Intersects;
            }
        }

        public readonly Vector4 GetCenter()
        {
            return (Max + Min) / 2f;
        }

        public readonly Vector4 GetDimensions()
        {
            return Max - Min;
        }

        public static BoundingBox4 Transform(BoundingBox4 box, Matrix4x4 mat)
        {
            box.GetCorners(out AlignedBoxCorners4 corners);

            Vector4 min = Vector4.Transform(corners.NearTopLeft, mat);
            Vector4 max = Vector4.Transform(corners.NearTopLeft, mat);

            min = Vector4.Min(min, Vector4.Transform(corners.NearTopRight, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.NearTopRight, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.NearBottomLeft, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.NearBottomLeft, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.NearBottomRight, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.NearBottomRight, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.FarTopLeft, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.FarTopLeft, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.FarTopRight, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.FarTopRight, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.FarBottomLeft, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.FarBottomLeft, mat));

            min = Vector4.Min(min, Vector4.Transform(corners.FarBottomRight, mat));
            max = Vector4.Max(max, Vector4.Transform(corners.FarBottomRight, mat));

            return new BoundingBox4(min, max);
        }

        public static BoundingBox4 CreateFromPoints(
            ReadOnlySpan<byte> pointBytes,
            int pointStride,
            Quaternion rotation,
            Vector3 offset,
            Vector3 scale)
        {
            nuint stride = (nuint)pointStride;
            nuint vertexCount = (nuint)pointBytes.Length / stride;
            if (vertexCount < 1)
            {
                return new BoundingBox4(new Vector4(offset, 0), new Vector4(offset, 0));
            }

            ref byte ptr = ref MemoryMarshal.GetReference(pointBytes);
            ref byte endPtr = ref Unsafe.Add(ref ptr, vertexCount * stride);

            Vector3 first = Unsafe.ReadUnaligned<Vector3>(ref ptr);
            ptr = ref Unsafe.Add(ref ptr, stride);

            Vector3 min = Vector3.Transform(first, rotation);
            Vector3 max = Vector3.Transform(first, rotation);

            while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr))
            {
                Vector3 point = Unsafe.ReadUnaligned<Vector3>(ref ptr);
                ptr = ref Unsafe.Add(ref ptr, stride);

                Vector3 pos = Vector3.Transform(point, rotation);

                if (min.X > pos.X)
                    min.X = pos.X;
                if (max.X < pos.X)
                    max.X = pos.X;

                if (min.Y > pos.Y)
                    min.Y = pos.Y;
                if (max.Y < pos.Y)
                    max.Y = pos.Y;

                if (min.Z > pos.Z)
                    min.Z = pos.Z;
                if (max.Z < pos.Z)
                    max.Z = pos.Z;
            }

            return new BoundingBox4(
                new Vector4((min * scale) + offset, 0),
                new Vector4((max * scale) + offset, 0));
        }

        public static BoundingBox4 CreateFromPoints(
            ReadOnlySpan<Vector3> points, Quaternion rotation, Vector3 offset, Vector3 scale)
        {
            if (points.Length == 0)
            {
                return new BoundingBox4(new Vector4(offset, 0), new Vector4(offset, 0));
            }

            Vector3 min = Vector3.Transform(points[0], rotation);
            Vector3 max = Vector3.Transform(points[0], rotation);

            for (int i = 1; i < points.Length; i++)
            {
                Vector3 pos = Vector3.Transform(points[i], rotation);

                if (min.X > pos.X)
                    min.X = pos.X;
                if (max.X < pos.X)
                    max.X = pos.X;

                if (min.Y > pos.Y)
                    min.Y = pos.Y;
                if (max.Y < pos.Y)
                    max.Y = pos.Y;

                if (min.Z > pos.Z)
                    min.Z = pos.Z;
                if (max.Z < pos.Z)
                    max.Z = pos.Z;
            }

            return new BoundingBox4(
                new Vector4((min * scale) + offset, 0),
                new Vector4((max * scale) + offset, 0));
        }

        public static BoundingBox4 CreateFromPoints(ReadOnlySpan<Vector3> vertices)
        {
            return CreateFromPoints(vertices, Quaternion.Identity, Vector3.Zero, Vector3.One);
        }

        public static BoundingBox4 Combine(BoundingBox4 box1, BoundingBox4 box2)
        {
            return new BoundingBox4(
                Vector4.Min(box1.Min, box2.Min),
                Vector4.Max(box1.Max, box2.Max));
        }

        public static bool operator ==(BoundingBox4 first, BoundingBox4 second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(BoundingBox4 first, BoundingBox4 second)
        {
            return !first.Equals(second);
        }

        public readonly bool Equals(BoundingBox4 other)
        {
            return Min == other.Min && Max == other.Max;
        }

        public override readonly string ToString()
        {
            return string.Format("Min:{0}, Max:{1}", Min, Max);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is BoundingBox4 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            int h1 = Min.GetHashCode();
            int h2 = Max.GetHashCode();
            uint shift5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)shift5 + h1) ^ h2;
        }

        public readonly void GetCorners(out AlignedBoxCorners4 corners)
        {
            corners.NearBottomLeft = new Vector4(Min.X, Min.Y, Max.Z, 0);
            corners.NearBottomRight = new Vector4(Max.X, Min.Y, Max.Z, 0);
            corners.NearTopLeft = new Vector4(Min.X, Max.Y, Max.Z, 0);
            corners.NearTopRight = Max;

            corners.FarBottomLeft = Min;
            corners.FarBottomRight = new Vector4(Max.X, Min.Y, Min.Z, 0);
            corners.FarTopLeft = new Vector4(Min.X, Max.Y, Min.Z, 0);
            corners.FarTopRight = new Vector4(Max.X, Max.Y, Min.Z, 0);
        }

        public readonly AlignedBoxCorners4 GetCorners()
        {
            GetCorners(out AlignedBoxCorners4 corners);
            return corners;
        }

        public readonly bool ContainsNaN()
        {
            return float.IsNaN(Min.X)
                || float.IsNaN(Min.Y)
                || float.IsNaN(Min.Z)
                || float.IsNaN(Max.X)
                || float.IsNaN(Max.Y)
                || float.IsNaN(Max.Z);
        }

    }
}