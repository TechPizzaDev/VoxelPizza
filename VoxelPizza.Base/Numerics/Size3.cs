using System;
using System.Diagnostics;

namespace VoxelPizza.Numerics
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct Size3 : IEquatable<Size3>
    {
        public uint W;
        public uint H;
        public uint D;

        public readonly uint Volume => W * H * D;

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

        public readonly override bool Equals(object? obj)
        {
            return obj is Size3 other && Equals(other);
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
    }
}
