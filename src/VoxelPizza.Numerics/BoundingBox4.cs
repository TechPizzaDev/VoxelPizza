using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace VoxelPizza.Numerics
{
    public readonly struct BoundingBox4 : IEquatable<BoundingBox4>
    {
        public readonly Vector4 Min;
        public readonly Vector4 Max;

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
            if (Vector128.LessThanAny(Max.AsVector128(), other.Min.AsVector128()) ||
                Vector128.GreaterThanAny(Min.AsVector128(), other.Max.AsVector128()))
            {
                return ContainmentType.Disjoint;
            }
            else if (
                Vector128.LessThanOrEqualAll(Min.AsVector128(), other.Min.AsVector128()) &&
                Vector128.GreaterThanOrEqualAll(Max.AsVector128(), other.Max.AsVector128()))
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

            Vector4 min = new(float.MaxValue);
            Vector4 max = new(float.MinValue);

            for (int i = 0; i < AlignedBoxCorners4.CornerCount; i++)
            {
                Vector4 corner = Unsafe.Add(ref corners.NearTopLeft, i);
                min = Vector4.Transform(corner, mat);
                max = Vector4.Transform(corner, mat);
            }

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

            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);

            while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr))
            {
                Vector3 point = Unsafe.ReadUnaligned<Vector3>(ref ptr);
                ptr = ref Unsafe.Add(ref ptr, stride);

                Vector3 pos = Vector3.Transform(point, rotation);

                min = Vector3.Min(min, pos);
                max = Vector3.Min(max, pos);
            }

            return new BoundingBox4(
                new Vector4((min * scale) + offset, 0),
                new Vector4((max * scale) + offset, 0));
        }

        public static BoundingBox4 CreateFromPoints(
            ReadOnlySpan<Vector3> points, Quaternion rotation, Vector3 offset, Vector3 scale)
        {
            return CreateFromPoints(MemoryMarshal.AsBytes(points), Unsafe.SizeOf<Vector3>(), rotation, offset, scale);
        }

        public static BoundingBox4 CreateFromPoints(ReadOnlySpan<Vector3> points)
        {
            if (points.Length == 0)
            {
                return new BoundingBox4(Vector4.Zero, Vector4.Zero);
            }

            Vector3 min = points[0];
            Vector3 max = points[0];

            for (int i = 1; i < points.Length; i++)
            {
                Vector3 pos = points[i];
                min = Vector3.Min(min, pos);
                max = Vector3.Min(max, pos);
            }

            return new BoundingBox4(new Vector4(min, 0), new Vector4(max, 0));
        }

        public static BoundingBox4 Combine(BoundingBox4 box1, BoundingBox4 box2)
        {
            return new BoundingBox4(
                Vector4.Min(box1.Min, box2.Min),
                Vector4.Max(box1.Max, box2.Max));
        }

        public static bool operator ==(BoundingBox4 first, BoundingBox4 second)
        {
            return first.Min == second.Min && first.Max == second.Max;
        }

        public static bool operator !=(BoundingBox4 first, BoundingBox4 second)
        {
            return first.Min != second.Min || first.Max != second.Max;
        }

        public readonly bool Equals(BoundingBox4 other)
        {
            return this == other;
        }

        public override readonly string ToString()
        {
            return $"Min:{Min}, Max:{Max}";
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is BoundingBox4 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            int h1 = Min.GetHashCode();
            int h2 = Max.GetHashCode();
            return HashCode.Combine(h1, h2);
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

        public readonly bool ContainsNaN()
        {
#pragma warning disable CS1718 // Comparison made to same variable
            return Min != Min || Max != Max; // NaN values are never equal
#pragma warning restore CS1718 // Comparison made to same variable
        }
    }
}