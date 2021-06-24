using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Numerics
{
    public struct BoundingBox : IEquatable<BoundingBox>
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public readonly ContainmentType Contains(in BoundingBox other)
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

        public readonly Vector3 GetCenter()
        {
            return (Max + Min) / 2f;
        }

        public readonly Vector3 GetDimensions()
        {
            return Max - Min;
        }

        public static BoundingBox Transform(BoundingBox box, Matrix4x4 mat)
        {
            box.GetCorners(out AlignedBoxCorners corners);

            Vector3 min = Vector3.Transform(corners.NearTopLeft, mat);
            Vector3 max = Vector3.Transform(corners.NearTopLeft, mat);

            min = Vector3.Min(min, Vector3.Transform(corners.NearTopRight, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.NearTopRight, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.NearBottomLeft, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.NearBottomLeft, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.NearBottomRight, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.NearBottomRight, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.FarTopLeft, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.FarTopLeft, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.FarTopRight, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.FarTopRight, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.FarBottomLeft, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.FarBottomLeft, mat));

            min = Vector3.Min(min, Vector3.Transform(corners.FarBottomRight, mat));
            max = Vector3.Max(max, Vector3.Transform(corners.FarBottomRight, mat));

            return new BoundingBox(min, max);
        }

        public static BoundingBox CreateFromPoints(
            ReadOnlySpan<byte> vertices,
            int vertexStride,
            Quaternion rotation,
            Vector3 offset,
            Vector3 scale)
        {
            int vertexCount = vertices.Length / vertexStride;
            if (vertexCount < 1)
            {
                return new BoundingBox(offset, offset);
            }

            ref byte vertexBytes = ref MemoryMarshal.GetReference(vertices);

            Vector3 firstVertex = Unsafe.ReadUnaligned<Vector3>(ref vertexBytes);
            Vector3 min = Vector3.Transform(firstVertex, rotation);
            Vector3 max = Vector3.Transform(firstVertex, rotation);

            nint nStride = vertexStride;
            for (nint i = 1; i < vertexCount; i++)
            {
                Vector3 vertex = Unsafe.ReadUnaligned<Vector3>(ref Unsafe.Add(ref vertexBytes, i * nStride));
                Vector3 pos = Vector3.Transform(vertex, rotation);

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

            return new BoundingBox((min * scale) + offset, (max * scale) + offset);
        }

        public static BoundingBox CreateFromVertices(
            ReadOnlySpan<Vector3> vertices, Quaternion rotation, Vector3 offset, Vector3 scale)
        {
            Vector3 min = Vector3.Transform(vertices[0], rotation);
            Vector3 max = Vector3.Transform(vertices[0], rotation);

            for (int i = 1; i < vertices.Length; i++)
            {
                Vector3 pos = Vector3.Transform(vertices[i], rotation);

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

            return new BoundingBox((min * scale) + offset, (max * scale) + offset);
        }

        public static BoundingBox CreateFromVertices(ReadOnlySpan<Vector3> vertices)
        {
            return CreateFromVertices(vertices, Quaternion.Identity, Vector3.Zero, Vector3.One);
        }

        public static BoundingBox Combine(BoundingBox box1, BoundingBox box2)
        {
            return new BoundingBox(
                Vector3.Min(box1.Min, box2.Min),
                Vector3.Max(box1.Max, box2.Max));
        }

        public static bool operator ==(BoundingBox first, BoundingBox second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(BoundingBox first, BoundingBox second)
        {
            return !first.Equals(second);
        }

        public readonly bool Equals(BoundingBox other)
        {
            return Min == other.Min && Max == other.Max;
        }

        public readonly override string ToString()
        {
            return string.Format("Min:{0}, Max:{1}", Min, Max);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is BoundingBox other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            int h1 = Min.GetHashCode();
            int h2 = Max.GetHashCode();
            uint shift5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)shift5 + h1) ^ h2;
        }

        public readonly void GetCorners(out AlignedBoxCorners corners)
        {
            corners.NearBottomLeft = new Vector3(Min.X, Min.Y, Max.Z);
            corners.NearBottomRight = new Vector3(Max.X, Min.Y, Max.Z);
            corners.NearTopLeft = new Vector3(Min.X, Max.Y, Max.Z);
            corners.NearTopRight = new Vector3(Max.X, Max.Y, Max.Z);

            corners.FarBottomLeft = new Vector3(Min.X, Min.Y, Min.Z);
            corners.FarBottomRight = new Vector3(Max.X, Min.Y, Min.Z);
            corners.FarTopLeft = new Vector3(Min.X, Max.Y, Min.Z);
            corners.FarTopRight = new Vector3(Max.X, Max.Y, Min.Z);
        }

        public readonly AlignedBoxCorners GetCorners()
        {
            GetCorners(out AlignedBoxCorners corners);
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