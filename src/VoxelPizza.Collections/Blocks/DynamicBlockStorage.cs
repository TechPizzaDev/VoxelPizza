using System;

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

    public override void SetBlock(int x, int y, int z, uint value)
    {
        PrepStorage(value);

        _storage.SetBlock(x, y, z, value);
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
