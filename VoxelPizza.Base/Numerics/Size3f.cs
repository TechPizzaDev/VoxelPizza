using System;
using System.Diagnostics;

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

        public readonly override bool Equals(object? obj)
        {
            return obj is Size3f other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(W, H, D);
        }

        public readonly override string ToString()
        {
            return $"W:{W} H:{H} D:{D}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
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
