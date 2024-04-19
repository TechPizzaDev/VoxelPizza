using System;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public sealed class BlockStorage16<T> : BlockStorage<T>
        where T : IBlockStorageDescriptor
    {
        private readonly ushort[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned16;

        public BlockStorage16()
        {
            _array = new ushort[(long)Height * Depth * Width];
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
            ushort value = _array[index];
            return value;
        }

        public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
        {
            Copy(offset, Size, new ReadOnlySpan<ushort>(_array), dstOffset, dstSize, dstSpan, size);
        }
        
        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            _array[index] = (ushort)value;
        }

        public override void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
        {
            Copy(srcOffset, srcSize, srcSpan, offset, Size, new Span<ushort>(_array), size);
        }

        public override void FillBlock(Int3 offset, Size3 size, uint value)
        {
            Fill(offset, size, (ushort)value, Size, new Span<ushort>(_array));
        }
    }
}
