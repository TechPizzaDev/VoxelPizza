using System;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public sealed class BlockStorage8<T> : BlockStorage<T>
        where T : IBlockStorageDescriptor
    {
        private readonly byte[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned8;

        public BlockStorage8()
        {
            _array = new byte[(long)Height * Depth * Width];
            IsEmpty = false;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = _array;
            storageType = StorageType;
            return true;
        }

        public override uint GetBlock(int x, int y, int z)
        {
            int index = GetIndex(x, y, z);
            byte value = _array[index];
            return value;
        }

        public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
        {
            Copy(offset, Size, new ReadOnlySpan<byte>(_array), dstOffset, dstSize, dstSpan, size);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            _array[index] = (byte)value;
        }

        public override void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
        {
            Copy(srcOffset, srcSize, srcSpan, offset, Size, new Span<byte>(_array), size);
        }

        public override void FillBlock(Int3 offset, Size3 size, uint value)
        {
            Fill(offset, size, (byte)value, Size, new Span<byte>(_array));
        }
    }
}
