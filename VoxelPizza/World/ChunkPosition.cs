using System;

namespace VoxelPizza.World
{
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

        public readonly bool Equals(ChunkPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is BlockPosition other && Equals(other);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
