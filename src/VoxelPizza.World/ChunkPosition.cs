using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.World
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct ChunkPosition : IEquatable<ChunkPosition>
    {
        public int X;
        public int Y;
        public int Z;

        public ChunkPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly BlockPosition ToBlock()
        {
            return new BlockPosition(Chunk.Width * X, Chunk.Height * Y, Chunk.Depth * Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ChunkRegionPosition ToRegion()
        {
            return new ChunkRegionPosition(
                ChunkRegion.ChunkToRegionX(X),
                ChunkRegion.ChunkToRegionY(Y),
                ChunkRegion.ChunkToRegionZ(Z));
        }

        public readonly bool Equals(ChunkPosition other)
        {
            return this == other;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ChunkPosition other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override readonly string ToString()
        {
            return $"X:{X} Y:{Y} Z:{Z}";
        }

        public readonly string ToNumericString()
        {
            return $"{X} {Y} {Z}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }

        public static bool operator ==(in ChunkPosition left, in ChunkPosition right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
        }

        public static bool operator !=(in ChunkPosition left, in ChunkPosition right)
        {
            return !(left == right);
        }

        public static ChunkPosition operator +(in ChunkPosition left, in ChunkPosition right)
        {
            return new ChunkPosition(
                left.X + right.X,
                left.Y + right.Y,
                left.Z + right.Z);
        }

        public static ChunkPosition operator -(in ChunkPosition left, in ChunkPosition right)
        {
            return new ChunkPosition(
                left.X - right.X,
                left.Y - right.Y,
                left.Z - right.Z);
        }
    }
}
