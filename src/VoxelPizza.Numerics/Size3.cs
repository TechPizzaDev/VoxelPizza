using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct Size3 : IEquatable<Size3>
    {
        public static Size3 Zero => default;

        public uint W;
        public uint H;
        public uint D;

        public readonly uint Volume => W * H * D;

        public readonly bool IsZero => Equals(Zero);

        public Size3(uint width, uint height, uint depth)
        {
            W = width;
            H = height;
            D = depth;
        }

        public Size3(uint size)
        {
            W = size;
            H = size;
            D = size;
        }

        public readonly bool Equals(Size3 other)
        {
            return W == other.W
                && H == other.H
                && D == other.D;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Size3 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(W, H, D);
        }

        public override readonly string ToString()
        {
            return $"W:{W} H:{H} D:{D}";
        }

        public readonly string ToSimpleString()
        {
            return $"{W}x{H}x{D}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }

        public readonly Int3 ToInt3()
        {
            return new Int3((int)W, (int)H, (int)D);
        }

        public static implicit operator Size3f(Size3 size)
        {
            return new Size3f(size.W, size.H, size.D);
        }

        public static implicit operator Vector3(Size3 size)
        {
            return new Vector3(size.W, size.H, size.D);
        }

        public static implicit operator Vector4(Size3 size)
        {
            return new Vector4(size.W, size.H, size.D, 0);
        }

        public static Size3 operator *(Size3 left, Size3 right)
        {
            return new Size3(left.W * right.W, left.H * right.H, left.D * right.D);
        }

        public static Size3 operator +(Size3 left, Size3 right)
        {
            return new Size3(left.W + right.W, left.H + right.H, left.D + right.D);
        }

        public static Size3 operator -(Size3 left, Size3 right)
        {
            return new Size3(left.W - right.W, left.H - right.H, left.D - right.D);
        }

        public static bool operator ==(Size3 left, Size3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Size3 left, Size3 right)
        {
            return !(left == right);
        }
    }
}
