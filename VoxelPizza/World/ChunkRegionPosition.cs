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

        public readonly bool Equals(ChunkRegionPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ChunkRegionPosition other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public readonly override string ToString()
        {
            return $"X:{X} Y:{Y} Z:{Z}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
