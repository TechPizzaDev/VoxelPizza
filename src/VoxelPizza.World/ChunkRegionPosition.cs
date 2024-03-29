using System;
using System.Diagnostics;

namespace VoxelPizza.World
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct ChunkRegionPosition : IEquatable<ChunkRegionPosition>
    {
        public int X;
        public int Y;
        public int Z;

        public ChunkRegionPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly BlockPosition ToBlock()
        {
            return new BlockPosition(
                ChunkRegion.Width * Chunk.Width * X,
                ChunkRegion.Height * Chunk.Height * Y,
                ChunkRegion.Depth * Chunk.Depth * Z);
        }

        public readonly ChunkPosition ToChunk()
        {
            return new ChunkPosition(
                ChunkRegion.Width * X,
                ChunkRegion.Height * Y,
                ChunkRegion.Depth * Z);
        }

        public readonly ChunkPosition OffsetLocalChunk(ChunkPosition localChunkPosition)
        {
            return new ChunkPosition(
                localChunkPosition.X + ChunkRegion.Width * X,
                localChunkPosition.Y + ChunkRegion.Height * Y,
                localChunkPosition.Z + ChunkRegion.Depth * Z);
        }

        public readonly bool Equals(ChunkRegionPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ChunkRegionPosition other && Equals(other);
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

        public static bool operator ==(ChunkRegionPosition left, ChunkRegionPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkRegionPosition left, ChunkRegionPosition right)
        {
            return !(left == right);
        }
    }
}
