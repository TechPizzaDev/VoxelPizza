using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public readonly struct ChunkBoxSlice(
        ChunkPosition chunk,
        BlockPosition block,
        BlockPosition innerOrigin,
        Size3 size) : IEquatable<ChunkBoxSlice>
    {
        public readonly ChunkPosition Chunk = chunk;
        public readonly BlockPosition Block = block;
        public readonly BlockPosition InnerOrigin = innerOrigin;
        public readonly Size3 Size = size;

        public bool Equals(ChunkBoxSlice other)
        {
            return Chunk == other.Chunk
                && Block == other.Block
                && InnerOrigin == other.InnerOrigin
                && Size == other.Size;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Chunk, Block, InnerOrigin, Size);
        }
    }
}
