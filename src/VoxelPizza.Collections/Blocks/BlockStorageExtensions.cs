using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public static class BlockStorageExtensions
{
    public static void GetBlockRow(this IReadableBlockStorage storage, int x, int y, int z, Span<uint> dstSpan)
    {
        int length = Math.Min(dstSpan.Length, (int)storage.Width - x);
        Size3 size = new((uint)length, 1, 1);
        Size3 dstSize = new((uint)dstSpan.Length, 1, 1);
        storage.GetBlocks(new Int3(x, y, z), size, new Int3(), dstSize, dstSpan);
    }

    public static void SetBlockRow(this IWritableBlockStorage storage, int x, int y, int z, ReadOnlySpan<uint> srcSpan)
    {
        int length = Math.Min(srcSpan.Length, (int)storage.Width - x);
        Size3 size = new((uint)length, 1, 1);
        Size3 srcSize = new((uint)srcSpan.Length, 1, 1);
        storage.SetBlocks(new Int3(x, y, z), size, new Int3(), srcSize, srcSpan);
    }

    public static void SetBlockLayer(this IWritableBlockStorage storage, int y, ReadOnlySpan<uint> srcSpan)
    {
        Size3 size = storage.Size with { H = 1 };
        storage.SetBlocks(new Int3(0, y, 0), size, new Int3(), size, srcSpan);
    }

    public static void SetBlocks(this IWritableBlockStorage storage, ReadOnlySpan<uint> srcSpan)
    {
        storage.SetBlocks(new Int3(), storage.Size, new Int3(), storage.Size, srcSpan);
    }

    public static void FillBlock(this IWritableBlockStorage storage, uint value)
    {
        storage.FillBlock(new Int3(), storage.Size, value);
    }
}
