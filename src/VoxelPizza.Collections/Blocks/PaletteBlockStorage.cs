using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using VoxelPizza.Collections.Bits;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public sealed class PaletteBlockStorage<T> : BlockStorage<T>
    where T : IBlockStorageDescriptor
{
    private const int StackThreshold = 256;

    private IndexMap<uint> _palette;

    private BitArray<ulong, int> _storage;

    public PaletteBlockStorage()
    {
        _palette = new IndexMap<uint>();
        _palette.Add(0);

        _storage = BitArray<ulong, int>.Allocate(Size.Volume, 1);
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
        int blockIndex = GetIndex(x, y, z);
        return GetBlock(blockIndex);
    }

    public uint GetBlock(int blockIndex)
    {
        BitArray<ulong, int> storage = _storage;
        BitArraySlot slot = storage.GetSlot(blockIndex);
        int index = storage.Get(slot);
        return _palette.Get(index);
    }

    [SkipLocalsInit]
    public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
    {
        int dstWidth = (int)dstSize.W;
        int dstDepth = (int)dstSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        int[]? bufArray = null;
        int stride = width;
        Span<int> bufSpan = stride <= StackThreshold
            ? stackalloc int[StackThreshold] : bufArray = ArrayPool<int>.Shared.Rent(stride);
        Span<int> buffer = bufSpan.Slice(0, stride);

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                int dstIdx = GetIndexBase(dstDepth, dstWidth, dstOffset.Y + y, dstOffset.Z + z);
                Span<uint> dst = dstSpan.Slice(dstIdx + dstOffset.X, stride);

                int srcIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z + z);
                GetContiguousBlocks(srcIdx, dst, buffer);
            }
        }
        
        if (bufArray != null)
        {
            ArrayPool<int>.Shared.Return(bufArray);
        }
    }

    private void GetContiguousBlocks(int srcIdx, Span<uint> dst, Span<int> buffer)
    {
        Debug.Assert(buffer.Length == dst.Length);

        BitArray<ulong, int> storage = _storage;
        ReadOnlySpan<uint> palette = _palette.AsSpan();
        Span<int> src = buffer;

        // TODO: Add BitArray.IndexOfAnyExcept to reduce unpacking?

        // Unpack block indices in bulk.
        storage.Get(srcIdx, src);

        if (src.Length <= 4)
        {
            for (int i = 0; i < src.Length; i++)
            {
                int index = src[i];
                uint value = palette[index];
                dst[i] = value;
            }
            return;
        }

        while (src.Length > 0)
        {
            int index = src[0];

            // Move ahead while there are duplicates in the source.
            int len = src.IndexOfAnyExcept(index);
            if (len == -1)
                len = src.Length; // Rest of source is same value.

            // Fill block values in bulk.
            Span<uint> values = dst.Slice(0, len);
            uint value = palette[index];
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
        BitArray<ulong, int> storage = _storage;
        BitArraySlot slot = storage.GetSlot(blockIndex);

        int prevIndex = storage.Get(slot);
        uint prevValue = _palette.Get(prevIndex);
        if (prevValue == value)
        {
            return false;
        }

        if (_palette.Add(value, out int index))
        {
            uint paletteCount = (uint)(_palette.Count - 1);
            int bitsNeeded = sizeof(uint) * 8 - BitOperations.LeadingZeroCount(paletteCount);
            if (storage.BitsPerElement != bitsNeeded)
            {
                SetBlockWithResize(blockIndex, value, bitsNeeded);
                return true;
            }
        }

        storage.Set(slot, index);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SetBlockWithResize(int blockIndex, uint value, int bitsPerElement)
    {
        throw new NotImplementedException();
    }

    [SkipLocalsInit]
    public override uint SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
    {
        int srcWidth = (int)srcSize.W;
        int srcDepth = (int)srcSize.D;

        int width = (int)size.W;
        int height = (int)size.H;
        int depth = (int)size.D;

        int[]? bufArray = null;
        uint changedCount = 0;

        if (depth == srcDepth && depth == Depth)
        {
            int stride = width * depth;
            Span<int> bufSpan = stride <= StackThreshold
                ? stackalloc int[StackThreshold] : bufArray = ArrayPool<int>.Shared.Rent(stride);
            Span<int> buffer = bufSpan.Slice(0, stride);

            for (int y = 0; y < height; y++)
            {
                int srcIdx = GetIndexBase(srcDepth, srcWidth, srcOffset.Y + y, srcOffset.Z);
                ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, stride);

                int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z);
                changedCount += SetContiguousBlocks(dstIdx, src, buffer);
            }
        }
        else
        {
            int stride = width;
            Span<int> bufSpan = stride <= StackThreshold
                ? stackalloc int[StackThreshold] : bufArray = ArrayPool<int>.Shared.Rent(stride);
            Span<int> buffer = bufSpan.Slice(0, stride);

            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int srcIdx = GetIndexBase(srcDepth, srcWidth, srcOffset.Y + y, srcOffset.Z + z);
                    ReadOnlySpan<uint> src = srcSpan.Slice(srcIdx + srcOffset.X, stride);

                    int dstIdx = offset.X + GetIndexBase(Depth, Width, offset.Y + y, offset.Z + z);
                    changedCount += SetContiguousBlocks(dstIdx, src, buffer);
                }
            }
        }

        if (bufArray != null)
        {
            ArrayPool<int>.Shared.Return(bufArray);
        }
        return changedCount;
    }

    private uint SetContiguousBlocks(int dstIdx, ReadOnlySpan<uint> source, Span<int> buffer)
    {
        Debug.Assert(buffer.Length == source.Length);

        BitArray<ulong, int> storage = _storage;
        IndexMap<uint> palette = _palette;

        // Unpack block indices in bulk.
        storage.Get(dstIdx, buffer);

        uint changedCount = 0;

        Span<int> dst = buffer;
        while (source.Length > 0)
        {
            uint value = source[0];
            palette.Add(value, out int index);

            // Move ahead while there are duplicates in the source.
            int len = source.IndexOfAnyExcept(value);
            if (len == -1)
                len = source.Length; // Rest of source is same value.

            Span<int> indices = dst.Slice(0, len);

            // Copy block indices in bulk (while counting changed blocks).
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> newIndices = Vector128.Create(index);

                while (indices.Length >= Vector128<int>.Count)
                {
                    Vector128<int> oldIndices = Vector128.Create((ReadOnlySpan<int>)indices);
                    newIndices.CopyTo(indices);

                    Vector128<int> equal = Vector128.Equals(oldIndices, newIndices);
                    changedCount += (uint)Vector128<int>.Count - equal.ExtractMostSignificantBits();

                    indices = indices.Slice(Vector128<int>.Count);
                }
            }

            // Copy remainder of block indices.
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] != index)
                {
                    indices[i] = index;

                    changedCount++;
                }
            }

            source = source.Slice(len);
            dst = dst.Slice(len);
        }

        // Pack block indices in bulk.
        storage.Set(dstIdx, buffer);

        return changedCount;
    }

    public override uint FillBlock(Int3 offset, Size3 size, uint value)
    {
        throw new NotImplementedException();
    }
}
