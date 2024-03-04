using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.World;

public readonly struct DimensionBoxSlice(
    ChunkRegionPosition region,
    BlockPosition block,
    BlockPosition innerOrigin,
    Size3 size) : IEquatable<DimensionBoxSlice>
{
    public readonly ChunkRegionPosition Region = region;
    public readonly BlockPosition Block = block;
    public readonly BlockPosition InnerOrigin = innerOrigin;
    public readonly Size3 Size = size;

    public (BlockPosition Origin, BlockPosition Max) GetOriginAndMax()
    {
        BlockPosition baseOrigin = Region.ToBlock();
        BlockPosition origin = baseOrigin + InnerOrigin;
        BlockPosition max = origin + new BlockPosition((int)Size.W, (int)Size.H, (int)Size.D);
        return (origin, max);
    }

    public bool Equals(DimensionBoxSlice other)
    {
        return Region == other.Region
            && Block == other.Block
            && InnerOrigin == other.InnerOrigin
            && Size == other.Size;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Region, Block, InnerOrigin, Size);
    }
}
