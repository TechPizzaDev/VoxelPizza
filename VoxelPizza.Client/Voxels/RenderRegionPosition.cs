using System;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public readonly struct RenderRegionPosition : IEquatable<RenderRegionPosition>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public RenderRegionPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public RenderRegionPosition(ChunkPosition chunkPosition, Size3 regionSize)
        {
            X = IntMath.DivideRoundDown(chunkPosition.X, (int)regionSize.W);
            Y = IntMath.DivideRoundDown(chunkPosition.Y, (int)regionSize.H);
            Z = IntMath.DivideRoundDown(chunkPosition.Z, (int)regionSize.D);
        }

        public readonly BlockPosition ToBlock(Size3 regionSize)
        {
            Size3 factor = Chunk.Size * regionSize;
            return new BlockPosition((int)factor.W * X, (int)factor.H * Y, (int)factor.D * Z);
        }


        public readonly ChunkPosition ToChunk(Size3 regionSize)
        {
            return new ChunkPosition((int)regionSize.W * X, (int)regionSize.H * Y, (int)regionSize.D * Z);
        }

        public bool Equals(RenderRegionPosition other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override string ToString()
        {
            return $"X:{X} Y:{Y} Z:{Z}";
        }
    }
}
