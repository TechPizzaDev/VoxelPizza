using System;

namespace VoxelPizza.Numerics
{
    public struct Byte4 : IEquatable<Byte4>
    {
        public byte X;
        public byte Y;
        public byte Z;
        public byte W;

        public Byte4(byte x, byte y, byte z, byte w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Byte4(Int3 xyz, byte w)
        {
            X = (byte)xyz.X;
            Y = (byte)xyz.Y;
            Z = (byte)xyz.Z;
            W = w;
        }

        public readonly bool Equals(Byte4 other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z
                && W == other.W;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, W);
        }
    }
}
