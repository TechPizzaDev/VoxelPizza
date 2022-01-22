using System;
using System.Diagnostics;
using System.Numerics;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct BlockPosition : IEquatable<BlockPosition>
    {
        public int X;
        public int Y;
        public int Z;

        public BlockPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static BlockPosition Abs(BlockPosition position)
        {
            return new BlockPosition(
                IntMath.Abs(position.X),
                IntMath.Abs(position.Y),
                IntMath.Abs(position.Z));
        }

        public readonly ChunkPosition ToChunk()
        {
            return new ChunkPosition(
                Chunk.BlockToChunkX(X),
                Chunk.BlockToChunkY(Y),
                Chunk.BlockToChunkZ(Z));
        }

        public readonly ChunkRegionPosition ToRegion()
        {
            return new ChunkRegionPosition(
                ChunkRegion.ChunkToRegionX(Chunk.BlockToChunkX(X)),
                ChunkRegion.ChunkToRegionY(Chunk.BlockToChunkY(Y)),
                ChunkRegion.ChunkToRegionZ(Chunk.BlockToChunkZ(Z)));
        }

        public readonly bool Equals(BlockPosition other)
        {
            return this == other;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is BlockPosition other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override readonly string ToString()
        {
            return $"X:{X} Y:{Y} Z:{Z}";
        }

        private readonly string GetDebuggerDisplay()
        {
            return ToString();
        }

        public static implicit operator Vector3(BlockPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }

        public static implicit operator Vector4(BlockPosition position)
        {
            return new Vector4(position.X, position.Y, position.Z, 0);
        }

        public static bool operator ==(in BlockPosition left, in BlockPosition right)
        {
            return left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z;
        }

        public static bool operator !=(in BlockPosition left, in BlockPosition right)
        {
            return left.X != right.X
                || left.Y != right.Y
                || left.Z != right.Z;
        }

        public static BlockPosition operator +(in BlockPosition left, in BlockPosition right)
        {
            return new BlockPosition(
                left.X + right.X,
                left.Y + right.Y,
                left.Z + right.Z);
        }

        public static BlockPosition operator -(in BlockPosition left, in BlockPosition right)
        {
            return new BlockPosition(
                left.X - right.X,
                left.Y - right.Y,
                left.Z - right.Z);
        }
    }
}
