using System;
using System.Runtime.CompilerServices;

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
            return ToInt32() == other.ToInt32();
        }

        public readonly int ToInt32()
        {
            return Unsafe.BitCast<Byte4, int>(this);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Byte4 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return ToInt32();
        }
    }
}
