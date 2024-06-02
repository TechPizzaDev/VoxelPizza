using System;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public sealed class BlockStorage32<T> : BlockStorage<T>
    where T : IBlockStorageDescriptor
{
    private readonly uint[] _array;

    public override BlockStorageType StorageType => BlockStorageType.Unsigned32;

    public BlockStorage32()
    {
        _array = new uint[(long)Height * Depth * Width];
        IsEmpty = false;
    }

    public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
    {
        inlineSpan = MemoryMarshal.AsBytes(_array.AsSpan());
        storageType = StorageType;
        return true;
    }

    public override uint GetBlock(int x, int y, int z)
    {
        int index = GetIndex(x, y, z);
        uint value = _array[index];
        return value;
    }

    public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
    {
        Copy(offset, Size, new ReadOnlySpan<uint>(_array), dstOffset, dstSize, dstSpan, size);
    }

    public override bool SetBlock(int x, int y, int z, uint value)
    {
        int index = GetIndex(x, y, z);
        ref uint slot = ref _array[index];
        if (slot != value)
        {
            slot = value;
            return true;
        }
        return false;
    }

    public override uint SetBlocks(
        Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
    {
        Copy(srcOffset, srcSize, srcSpan, offset, Size, new Span<uint>(_array), size);
        return size.Volume; // TODO
    }

    public override uint FillBlock(
        Int3 offset, Size3 size, uint value, ChangeTracking changeTracking)
    {
        Fill(offset, size, value, Size, new Span<uint>(_array));
        return size.Volume; // TODO
    }
}
