using System;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public sealed class BlockStorage24<T> : BlockStorage<T>
        where T : IBlockStorageDescriptor
    {
        private readonly UInt24[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned24;

        public BlockStorage24()
        {
            _array = new UInt24[(long)Height * Depth * Width];
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
            UInt24 value = _array[index];
            return value;
        }

        public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
        {
            Copy(offset, Size, new ReadOnlySpan<UInt24>(_array), dstOffset, dstSize, dstSpan, size);
        }

        public override bool SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            ref UInt24 slot = ref _array[index];
            UInt24 packed = (UInt24)value;
            if (slot != packed)
            {
                slot = packed;
                return true;
            }
            return false;
        }

        public override uint SetBlocks(
            Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan, ChangeTracking changeTracking)
        {
            Copy(srcOffset, srcSize, srcSpan, offset, Size, new Span<UInt24>(_array), size);
            return size.Volume; // TODO
        }

        public override uint FillBlock(
            Int3 offset, Size3 size, uint value, ChangeTracking changeTracking)
        {
            Fill(offset, size, (UInt24)value, Size, new Span<UInt24>(_array));
            return size.Volume; // TODO
        }
    }
}
