using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public sealed class DynamicBlockStorage<T> : BlockStorage<T>
    where T : IBlockStorageDescriptor
{
    private static BlockStorage0<T> EmptyStorage { get; } = new(0);

    private BlockStorage<T> _storage;

    public DynamicBlockStorage()
    {
        _storage = EmptyStorage;
    }

    public override BlockStorageType StorageType => _storage.StorageType;

    public override uint GetBlock(int x, int y, int z)
    {
        return _storage.GetBlock(x, y, z);
    }

    public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
    {
        _storage.GetBlocks(offset, size, dstOffset, dstSize, dstSpan);
    }

    public override void SetBlock(int x, int y, int z, uint value)
    {
        PrepStorage(value);

        _storage.SetBlock(x, y, z, value);
    }

    public override void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
    {
        PrepStorage(srcSpan);

        _storage.SetBlocks(offset, size, srcOffset, srcSize, srcSpan);
    }

    public override void FillBlock(Int3 offset, Size3 size, uint value)
    {
        PrepStorage(value);

        _storage.FillBlock(offset, size, value);
    }

    private void PrepStorage(ReadOnlySpan<uint> values)
    {
        if (_storage != EmptyStorage)
        {
            return;
        }
        
        if (values.IndexOfAnyExcept(0u) != -1)
        {
            _storage = new BlockStorage8<T>();
        }
    }

    private void PrepStorage(uint value)
    {
        PrepStorage(new ReadOnlySpan<uint>(in value));
    }

    public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
    {
        return _storage.TryGetInline(out inlineSpan, out storageType);
    }
}
