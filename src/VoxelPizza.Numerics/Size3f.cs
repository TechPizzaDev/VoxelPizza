using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct Size3f : IEquatable<Size3f>
    {
        public float W;
        public float H;
        public float D;

        public readonly float Volume => W * H * D;

        public Size3f(float width, float height, float depth)
        {
            W = width;
            H = height;
            D = depth;
        }

        public Size3f(float size)
        {
            W = size;
            H = size;
            D = size;
        }

        public readonly bool Equals(Size3f other)
        {
            return W == other.W
                && H == other.H
                && D == other.D;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Size3f other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(W, H, D);
        }

        public override readonly string ToString()
        {
            return $"W:{W} H:{H} D:{D}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }

        public static implicit operator Vector3(Size3f size)
        {
            return new Vector3(size.W, size.H, size.D);
        }

        public static Size3f operator *(Size3f left, Size3f right)
        {
            return new Size3f(left.W * right.W, left.H * right.H, left.D * right.D);
        }

        public static Size3f operator +(Size3f left, Size3f right)
        {
            return new Size3f(left.W + right.W, left.H + right.H, left.D + right.D);
        }

        public static Size3f operator -(Size3f left, Size3f right)
        {
            return new Size3f(left.W - right.W, left.H - right.H, left.D - right.D);
        }
    }
}
