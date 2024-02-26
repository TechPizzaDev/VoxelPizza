using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct Int3 : IEquatable<Int3>
    {
        public int X;
        public int Y;
        public int Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly bool IsNegative()
        {
            return X < 0 || Y < 0 || Z < 0;
        }

        public readonly bool Equals(Int3 other)
        {
            return this == other;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Int3 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static implicit operator Vector3(Int3 size)
        {
            return new Vector3(size.X, size.Y, size.Z);
        }

        public static implicit operator Vector4(Int3 size)
        {
            return new Vector4(size.X, size.Y, size.Z, 0);
        }

        public static explicit operator Int3(Vector3 size)
        {
            return new Int3((int)size.X, (int)size.Y, (int)size.Z);
        }

        public static Int3 operator *(Int3 left, Int3 right)
        {
            return new Int3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
        }

        public static Int3 operator +(Int3 left, Int3 right)
        {
            return new Int3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Int3 operator -(Int3 left, Int3 right)
        {
            return new Int3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static bool operator ==(Int3 left, Int3 right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
        }

        public static bool operator !=(Int3 left, Int3 right)
        {
            return left.X != right.X
                || left.Y != right.Y
                || left.Z != right.Z;
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }

        public override readonly string ToString()
        {
            return $"<{X}, {Y}, {Z}>";
        }
    }
}
