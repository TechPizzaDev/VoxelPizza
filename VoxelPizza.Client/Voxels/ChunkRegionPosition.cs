using System;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public readonly struct ChunkRegionPosition : IEquatable<ChunkRegionPosition>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ChunkRegionPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly BlockPosition ToBlock(Size3 regionSize)
        {
            Size3 factor = Chunk.Size * regionSize;
            return new BlockPosition((int)factor.W * X, (int)factor.H * Y, (int)factor.D * Z);
        }

        public bool Equals(ChunkRegionPosition other)
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
