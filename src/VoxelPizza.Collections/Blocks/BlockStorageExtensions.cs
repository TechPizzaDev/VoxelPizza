using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public static class BlockStorageExtensions
{
    public static void GetBlockRow(
        this IReadableBlockStorage storage, int x, int y, int z, Span<uint> dstSpan)
    {
        int length = Math.Min(dstSpan.Length, storage.Width - x);
        Size3 size = new((uint)length, 1, 1);
        storage.GetBlocks(new Int3(x, y, z), size, new Int3(0), size, dstSpan);
    }

    public static uint SetBlockRow(
        this IWritableBlockStorage storage, int x, int y, int z, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
    {
        int length = Math.Min(srcSpan.Length, storage.Width - x);
        Size3 size = new((uint)length, 1, 1);
        uint changeCount = storage.SetBlocks(new Int3(x, y, z), size, new Int3(0), size, srcSpan, changeTracking);
        return changeCount;
    }

    public static void SetBlockRow(
        this IWritableBlockStorage storage, int x, int y, int z, ReadOnlySpan<uint> srcSpan)
    {
        SetBlockRow(storage, x, y, z, srcSpan, ChangeTracking.None);
    }

    public static uint SetBlockLayer(
        this IWritableBlockStorage storage, int y, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
    {
        Size3 size = storage.Size with { H = 1 };
        uint changeCount = storage.SetBlocks(new Int3(0, y, 0), size, new Int3(0), size, srcSpan, changeTracking);
        return changeCount;
    }

    public static void SetBlockLayer(
        this IWritableBlockStorage storage, int y, ReadOnlySpan<uint> srcSpan)
    {
        SetBlockLayer(storage, y, srcSpan, ChangeTracking.None);
    }
    
    public static uint SetBlocks(
        this IWritableBlockStorage storage, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
    {
        uint changeCount = storage.SetBlocks(new Int3(0), storage.Size, new Int3(0), storage.Size, srcSpan, changeTracking);
        return changeCount;
    }

    public static void SetBlocks(
        this IWritableBlockStorage storage, ReadOnlySpan<uint> srcSpan)
    {
        SetBlocks(storage, srcSpan, ChangeTracking.None);
    }

    public static uint FillBlock(
        this IWritableBlockStorage storage, uint value, ChangeTracking changeTracking)
    {
        uint changeCount = storage.FillBlock(new Int3(0), storage.Size, value, changeTracking);
        return changeCount;
    }

    public static void FillBlock(
        this IWritableBlockStorage storage, uint value)
    {
        FillBlock(storage, value, ChangeTracking.None);
    }
}
