using System;
using System.Diagnostics;

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

        public readonly ChunkRegionPosition ToRegion()
        {
            return new ChunkRegionPosition(
                ChunkRegion.ChunkToRegionX(X),
                ChunkRegion.ChunkToRegionY(Y),
                ChunkRegion.ChunkToRegionZ(Z));
        }

        public readonly bool Equals(ChunkPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ChunkPosition other && Equals(other);
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

        public static bool operator ==(ChunkPosition left, ChunkPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkPosition left, ChunkPosition right)
        {
            return !(left == right);
        }
    }
}
