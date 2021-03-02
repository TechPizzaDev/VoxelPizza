using System;

namespace VoxelPizza.World
{
    public readonly struct ChunkPosition : IEquatable<ChunkPosition>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ChunkPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(ChunkPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
