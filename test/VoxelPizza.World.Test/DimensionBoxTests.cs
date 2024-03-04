using System.Collections.Generic;
using VoxelPizza.Numerics;
using Xunit;

namespace VoxelPizza.World.Test;

public class DimensionBoxTests
{
    public static TheoryData<BlockPosition, BlockPosition> TestPositions => new()
    {
        { new BlockPosition(-16, -16, -16), new BlockPosition(0, 0, 0) },
        { new BlockPosition(-2, -2, -2), new BlockPosition(18, 18, 18) },
    };

    [Theory, MemberData(nameof(TestPositions))]
    public void EqualSlicing(BlockPosition origin, BlockPosition max)
    {
        DimensionBox box = new(origin, max);

        DimensionBox.Enumerator dimEnum = new(box.Origin, box.Max);
        Assert.Equal(box.Size, dimEnum.Size);

        ChunkBoxSliceEnumerator chunkEnum = new(box.Origin, box.Max);
        Assert.Equal(box.Size, chunkEnum.Size);

        List<DimensionBoxSlice> dimList = [.. dimEnum];

        List<ChunkBoxSlice> chunkList = new();
        foreach (ChunkBoxSlice chunkSlice in chunkEnum)
        {
            ChunkBoxSlice newSlice = new(chunkSlice.Chunk, chunkSlice.Block - chunkEnum.Origin, chunkSlice.InnerOrigin, chunkSlice.Size);
            chunkList.Add(newSlice);
        }

        List<ChunkBoxSlice> chunkListFromDim = new();
        foreach (DimensionBoxSlice dimSlice in dimList)
        {
            (BlockPosition dimOrigin, BlockPosition dimMax) = dimSlice.GetOriginAndMax();
            foreach (ChunkBoxSlice chunkSlice in new ChunkBoxSliceEnumerator(dimOrigin, dimMax))
            {
                ChunkBoxSlice newSlice = new(chunkSlice.Chunk, chunkSlice.Block - box.Origin, chunkSlice.InnerOrigin, chunkSlice.Size);
                chunkListFromDim.Add(newSlice);
            }
        }

        chunkList.Sort(SliceComparer);
        chunkListFromDim.Sort(SliceComparer);

        Assert.Equal(chunkList, chunkListFromDim);
    }

    private static int SliceComparer(ChunkBoxSlice a, ChunkBoxSlice b)
    {
        int first = Check(a.Chunk.ToInt3(), b.Chunk.ToInt3());
        if (first != 0)
        {
            return first;
        }
        return Check(a.Block.ToInt3(), b.Block.ToInt3());

        static int Check(Int3 pos1, Int3 pos2)
        {
            int a = pos1.X.CompareTo(pos2.X);
            if (a != 0)
            {
                return a;
            }
            int b = pos1.Y.CompareTo(pos2.Y);
            if (b != 0)
            {
                return b;
            }
            return pos1.Z.CompareTo(pos2.Z);
        }
    }
}
