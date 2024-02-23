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

        public static int GetChunkIndex(ChunkPosition chunkPosition, Size3 regionSize)
        {
            return (chunkPosition.Y * (int)regionSize.D + chunkPosition.Z) * (int)regionSize.W + chunkPosition.X;
        }

        public static ChunkPosition GetLocalChunkPosition(ChunkPosition chunkPosition, Size3 regionSize)
        {
            int x = chunkPosition.X % (int)regionSize.W;
            if (x < 0)
                x = (int)regionSize.W + x;

            int y = chunkPosition.Y % (int)regionSize.H;
            if (y < 0)
                y = (int)regionSize.H + y;

            int z = chunkPosition.Z % (int)regionSize.D;
            if (z < 0)
                z = (int)regionSize.D + z;

            return new ChunkPosition(x, y, z);
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

        public override bool Equals(object? obj)
        {
            return obj is RenderRegionPosition other && Equals(other);
        }

        public static bool operator ==(RenderRegionPosition left, RenderRegionPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RenderRegionPosition left, RenderRegionPosition right)
        {
            return !(left == right);
        }
    }
}
