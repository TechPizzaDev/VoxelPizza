using System;

namespace VoxelPizza.Numerics
{
    public struct UInt3 : IEquatable<UInt3>
    {
        public uint X;
        public uint Y;
        public uint Z;

        public UInt3(uint x, uint y, uint z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly bool Equals(UInt3 other)
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
