using System;

namespace VoxelPizza.Numerics
{
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
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
