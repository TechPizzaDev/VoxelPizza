using System;
using System.Buffers;
using System.Diagnostics;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public sealed class PaletteBlockStorage<T> : BlockStorage<T>
    where T : IBlockStorageDescriptor
{
    private IndexMap<uint> _palette;

    private BlockStorage8<T> _tmpStorage;

    public PaletteBlockStorage()
    {
        _palette = new IndexMap<uint>();
        _palette.Add(0);

        _tmpStorage = new BlockStorage8<T>();
    }

    public override BlockStorageType StorageType => BlockStorageType.Specialized;

    public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
    {
        inlineSpan = Span<byte>.Empty;
        storageType = StorageType;
        return false;
    }

    public override uint GetBlock(int x, int y, int z)
    {
        int index = (int)_tmpStorage.GetBlock(x, y, z);
        return _palette.ValueForIndex(index);
    }

    public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
    {
        _tmpStorage.GetBlocks(offset, size, dstOffset, dstSize, dstSpan);

        int dstWidth = (int)dstSize.W;
        int dstDepth = (int)dstSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                int dstIdx = GetIndexBase(dstWidth, dstDepth, dstOffset.Y + y, dstOffset.Z + z);
                Span<uint> dst = dstSpan.Slice(dstIdx + dstOffset.X, width);

                for (int i = 0; i < dst.Length; i++)
                {
                    uint index = dst[i];
                    uint value = _palette.ValueForIndex((int)index);
                    dst[i] = value;
                }
            }
        }
    }

    public override void SetBlock(int x, int y, int z, uint value)
    {
        PrepStorage(new Int3(x, y, z), new Size3(1), value);

        int index = _palette.IndexForValue(value);
        _tmpStorage.SetBlock(x, y, z, (uint)index);
    }

    public override void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
    {
        PrepStorage(offset, size, srcOffset, srcSize, srcSpan);

        _tmpStorage.SetBlocks(offset, size, srcOffset, srcSize, srcSpan);

        int count = (int)size.Volume;
        uint[] buffer = ArrayPool<uint>.Shared.Rent(count);
        Span<uint> span = buffer.AsSpan(0, count);

        Copy(srcOffset, srcSize, srcSpan, new Int3(0), size, span, size);

        for (int i = 0; i < span.Length; i++)
        {
            uint value = span[i];
            int index = _palette.IndexForValue(value);
            span[i] = (uint)index;
        }

        _tmpStorage.SetBlocks(offset, size, new Int3(0), size, buffer);

        ArrayPool<uint>.Shared.Return(buffer);
    }

    public override void FillBlock(Int3 offset, Size3 size, uint value)
    {
        PrepStorage(offset, size, value);

        int index = _palette.IndexForValue(value);
        _tmpStorage.FillBlock(offset, size, (uint)index);
    }

    private void PrepStorage(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
    {
        ReturnAreaToPool(offset, size);

        int srcWidth = (int)srcSize.W;
        int srcDepth = (int)srcSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                int srcIdx = GetIndexBase(srcWidth, srcDepth, srcOffset.Y + y, srcOffset.Z + z);
                ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, width);

                for (int i = 0; i < src.Length; i++)
                {
                    uint value = src[i];

                    // Move ahead while there are duplicates
                    while ((uint)(i + 1) < (uint)src.Length && value == src[i + 1])
                    {
                        i++;
                    }

                    _palette.Add(value);
                }
            }
        }
    }

    private void PrepStorage(Int3 offset, Size3 size, uint value)
    {
        ReturnAreaToPool(offset, size);

        _palette.Add(value);
    }

    private void ReturnAreaToPool(Int3 offset, Size3 size)
    {
        int count = (int)size.Volume;
        uint[] buffer = ArrayPool<uint>.Shared.Rent(count);
        Span<uint> span = buffer.AsSpan(0, count);

        GetBlocks(offset, size, new Int3(0), size, span);

        for (int i = 0; i < span.Length; i++)
        {
            uint value = span[i];

            // Move ahead while there are duplicates
            while ((uint)(i + 1) < (uint)span.Length && value == span[i + 1])
            {
                i++;
            }

            bool c = _palette.Contains(value);
            Debug.Assert(c);
        }

        ArrayPool<uint>.Shared.Return(buffer);
    }

    private static bool EqualTo(Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan, uint value)
    {
        int srcWidth = (int)srcSize.W;
        int srcDepth = (int)srcSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                int srcIdx = GetIndexBase(srcWidth, srcDepth, srcOffset.Y + y, srcOffset.Z + z);
                ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, width);

                int index = src.IndexOfAnyExcept(value);
                if (index != -1)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
