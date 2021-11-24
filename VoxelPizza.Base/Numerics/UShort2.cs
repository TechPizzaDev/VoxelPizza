using System;

namespace VoxelPizza.Numerics
{
    public struct UShort2 : IEquatable<UShort2>
    {
        public ushort X;
        public ushort Y;

        public UShort2(ushort x, ushort y)
        {
            X = x;
            Y = y;
        }

        public readonly bool Equals(UShort2 other)
        {
            return X == other.X
                && Y == other.Y;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
    }
}
