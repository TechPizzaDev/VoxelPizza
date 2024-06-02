using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using VoxelPizza.Collections.Bits;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public sealed class PaletteBlockStorage<TDescriptor> : BlockStorage<TDescriptor>
    where TDescriptor : IBlockStorageDescriptor
{
    private const int StackThreshold = 1024;

    private readonly IndexMap<uint> _palette;

    private BitArray<ulong> _storage;

    public PaletteBlockStorage(IndexMap<uint> palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        _palette = palette;

        int bitsPerElement = GetStorageBitsForPalette(palette.Count);
        _storage = BitArray<ulong>.Allocate(Size.Volume, bitsPerElement);
    }

    public override BlockStorageType StorageType => BlockStorageType.Specialized;

    private static int GetStorageBitsForPalette(int count)
    {
        if (count <= 1)
        {
            return 1;
        }
        return sizeof(uint) * 8 - BitOperations.LeadingZeroCount((uint)(count - 1));
    }

    public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
    {
        inlineSpan = Span<byte>.Empty;
        storageType = StorageType;
        return false;
    }

    public override uint GetBlock(int x, int y, int z)
    {
        int blockIndex = GetIndex(x, y, z);
        return GetBlock(blockIndex);
    }

    public uint GetBlock(int blockIndex)
    {
        BitArray<ulong> storage = _storage;
        BitArraySlot slot = storage.GetSlot(blockIndex);
        uint index = storage.Get<uint>(slot);
        return _palette[(int)index];
    }

    [SkipLocalsInit]
    public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
    {
        switch (_storage.BitsPerElement)
        {
            case <= 08: GetBlocksCore<byte>(offset, size, dstOffset, dstSize, dstSpan); break;
            case <= 16: GetBlocksCore<ushort>(offset, size, dstOffset, dstSize, dstSpan); break;
            case <= 32: GetBlocksCore<uint>(offset, size, dstOffset, dstSize, dstSpan); break;
            default: ThrowUnsupportedElementSize(); break;
        }
    }

    private unsafe void GetBlocksCore<E>(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
        where E : unmanaged, IBinaryInteger<E>
    {
        int dstWidth = (int)dstSize.W;
        int dstDepth = (int)dstSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        int stride = width;
        int threshold = StackThreshold / sizeof(E);

        E[]? indexArray = null;
        Span<E> indexBuffer = stride <= threshold ? stackalloc E[threshold] : indexArray = ArrayPool<E>.Shared.Rent(stride);
        indexBuffer = indexBuffer.Slice(0, stride);

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                int dstIdx = GetIndexBase(dstDepth, dstWidth, dstOffset.Y + y, dstOffset.Z + z);
                Span<uint> dst = dstSpan.Slice(dstIdx + dstOffset.X, stride);

                int srcIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z + z);
                GetContiguousBlocks(srcIdx, dst, indexBuffer);
            }
        }

        if (indexArray != null)
        {
            ArrayPool<E>.Shared.Return(indexArray);
        }
    }

    private void GetContiguousBlocks<E>(int srcIdx, Span<uint> dst, Span<E> buffer)
        where E : unmanaged, IBinaryInteger<E>
    {
        Debug.Assert(buffer.Length == dst.Length);

        BitArray<ulong> storage = _storage;
        ReadOnlySpan<uint> palette = _palette.AsSpan();
        Span<E> src = buffer;

        // TODO: Add BitArray.IndexOfAnyExcept to reduce unpacking?

        // Unpack block indices in bulk.
        storage.GetRange(srcIdx, src);

        if (src.Length < Vector128<E>.Count)
        {
            for (int i = 0; i < src.Length; i++)
            {
                E index = src[i];
                uint value = palette[int.CreateTruncating(index)];
                dst[i] = value;
            }
            return;
        }

        while (src.Length > 0)
        {
            E index = src[0];

            // Move ahead while there are duplicates in the source.
            int len = src.IndexOfAnyExcept(index);
            if (len == -1)
                len = src.Length; // Rest of source is same value.

            // Fill block values in bulk.
            Span<uint> values = dst.Slice(0, len);
            uint value = palette[int.CreateTruncating(index)];
            values.Fill(value);

            src = src.Slice(len);
            dst = dst.Slice(len);
        }
    }

    public override bool SetBlock(int x, int y, int z, uint value)
    {
        int blockIndex = GetIndex(x, y, z);
        return SetBlock(blockIndex, value);
    }

    public bool SetBlock(int blockIndex, uint value)
    {
        BitArray<ulong> storage = _storage;
        BitArraySlot slot = storage.GetSlot(blockIndex);

        uint prevIndex = storage.Get<uint>(slot);
        uint prevValue = _palette[(int)prevIndex];
        if (prevValue == value)
        {
            return false;
        }

        if (!_palette.TryAdd(value, out int index))
        {
            storage.Set(slot, (uint)index);
            return true;
        }

        int bitsNeeded = GetStorageBitsForPalette(_palette.Count);
        if (storage.BitsPerElement != bitsNeeded)
        {
            SetBlockWithResize(blockIndex, value, bitsNeeded);
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SetBlockWithResize(int blockIndex, uint value, int bitsPerElement)
    {
        ResizeStorage(bitsPerElement);

        BitArraySlot slot = _storage.GetSlot(blockIndex);
        int index = _palette.IndexOf(value);
        Debug.Assert(index != -1, "Palette is missing value.");
        _storage.Set(slot, index);
    }

    private void ResizeStorage(int bitsPerElement)
    {
        BitArray<ulong> newStorage = BitArray<ulong>.AllocateUninitialized(Size.Volume, bitsPerElement);

        _storage.AsBitSpan().CopyTo(newStorage.AsBitSpan());

        _storage = newStorage;
    }

    private void TryAddPaletteValue(uint value, out int paletteIndex)
    {
        if (_palette.TryAdd(value, out paletteIndex))
        {
            int bitsNeeded = GetStorageBitsForPalette(_palette.Count);
            if (_storage.BitsPerElement != bitsNeeded)
            {
                ResizeStorage(bitsNeeded);
            }
        }
    }

    public override uint SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
    {
        uint firstValue = srcSpan[0];
        int runLength = srcSpan.IndexOfAnyExcept(firstValue);
        if (runLength == -1)
        {
            return FillBlock(offset, size, firstValue, changeTracking);
        }

        int addedCountEstimate = srcSpan.Length - runLength;
        int bitsNeededEstimate = GetStorageBitsForPalette(_palette.Count + addedCountEstimate);

        nint changedCount = bitsNeededEstimate switch
        {
            <= 08 => SetBlocksCore<byte>(offset, size, srcOffset, srcSize, srcSpan, changeTracking),
            <= 16 => SetBlocksCore<ushort>(offset, size, srcOffset, srcSize, srcSpan, changeTracking),
            <= 32 => SetBlocksCore<uint>(offset, size, srcOffset, srcSize, srcSpan, changeTracking),
            _ => ThrowUnsupportedElementSize(),
        };
        return (uint)(nuint)changedCount;
    }

    [SkipLocalsInit]
    private unsafe nint SetBlocksCore<E>(
        Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
        where E : unmanaged, IBinaryInteger<E>
    {
        int srcWidth = (int)srcSize.W;
        int srcDepth = (int)srcSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        nint changedCount = 0;

        int stride = (depth == srcDepth && depth == Depth) ? (width * depth) : width;
        int threshold = StackThreshold / sizeof(E);

        E[]? indexArray = null;
        Span<E> indexBuffer = stride <= threshold ? stackalloc E[threshold] : indexArray = ArrayPool<E>.Shared.Rent(stride);
        indexBuffer = indexBuffer.Slice(0, stride);

        if (depth == srcDepth && depth == Depth)
        {
            for (int y = 0; y < height; y++)
            {
                int srcIdx = GetIndexBase(srcDepth, srcWidth, srcOffset.Y + y, srcOffset.Z);
                ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, stride);

                int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z);
                changedCount += SetContiguousBlocks(dstIdx, src, indexBuffer, changeTracking);
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int srcIdx = GetIndexBase(srcDepth, srcWidth, srcOffset.Y + y, srcOffset.Z + z);
                    ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, stride);

                    int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z + z);
                    changedCount += SetContiguousBlocks(dstIdx, src, indexBuffer, changeTracking);
                }
            }
        }

        if (indexArray != null)
            ArrayPool<E>.Shared.Return(indexArray);

        return changedCount;
    }

    private nint SetContiguousBlocks<E>(
        int dstIdx, ReadOnlySpan<uint> source, Span<E> indexBuffer, ChangeTracking changeTracking)
        where E : unmanaged, IBinaryInteger<E>
    {
        Debug.Assert(indexBuffer.Length == source.Length);

        BitArray<ulong> storage = _storage;

        // Unpack block indices in bulk.
        storage.GetRange(dstIdx, indexBuffer);

        Span<E> dst = indexBuffer;
        while (source.Length > 0)
        {
            uint value = source[0];
            TryAddPaletteValue(value, out int palIndex);

            // Move ahead while there are duplicates in the source.
            int len = source.IndexOfAnyExcept(value);
            if (len == -1)
                len = source.Length; // Rest of source is same value.

            E index = E.CreateChecked((uint)palIndex);

            Span<E> indices = dst.Slice(0, len);

            // Copy block indices in bulk.
            indices.Fill(index);

            source = source.Slice(len);
            dst = dst.Slice(len);
        }

        // Pack block indices in bulk.
        nint changedCount = storage.SetRange<E>(dstIdx, indexBuffer, changeTracking);

        return changedCount;
    }

    public override uint FillBlock(Int3 offset, Size3 size, uint value, ChangeTracking changeTracking)
    {
        TryAddPaletteValue(value, out int palIndex);

        nint changedCount = _storage.BitsPerElement switch
        {
            <= 08 => FillBlockCore<byte>(offset, size, palIndex, changeTracking),
            <= 16 => FillBlockCore<ushort>(offset, size, palIndex, changeTracking),
            <= 32 => FillBlockCore<uint>(offset, size, palIndex, changeTracking),
            _ => ThrowUnsupportedElementSize(),
        };
        return (uint)(nuint)changedCount;
    }

    private nint FillBlockCore<E>(Int3 offset, Size3 size, int paletteIndex, ChangeTracking changeTracking)
        where E : unmanaged, IBinaryInteger<E>
    {
        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        nint changedCount = 0;
        E index = E.CreateChecked(paletteIndex);

        if (depth == Depth)
        {
            int stride = width * depth;
            for (int y = 0; y < height; y++)
            {
                int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z);
                changedCount += FillContiguousBlocks(dstIdx, stride, index, changeTracking);
            }
        }
        else
        {
            int stride = width;
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z + z);
                    changedCount += FillContiguousBlocks(dstIdx, stride, index, changeTracking);
                }
            }
        }

        return changedCount;
    }

    private nint FillContiguousBlocks<E>(int dstIdx, int count, E value, ChangeTracking changeTracking)
        where E : unmanaged, IBinaryInteger<E>
    {
        nint changeCount = _storage.AsBitSpan(dstIdx, count).Fill(value, changeTracking);
        return changeCount;
    }

    [DoesNotReturn]
    private static nint ThrowUnsupportedElementSize() => throw new NotSupportedException();
}
