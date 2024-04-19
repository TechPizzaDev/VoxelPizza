using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    public sealed class BlockStorage0<T> : BlockStorage<T>
        where T : IBlockStorageDescriptor
    {
        private uint _value;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned0;

        public BlockStorage0(uint value)
        {
            _value = value;
            IsEmpty = value == 0;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = MemoryMarshal.AsBytes(new Span<uint>(ref _value));
            storageType = StorageType;
            return true;
        }

        public override void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan)
        {
            // TODO: validate ranges
            Fill(dstOffset, size, _value, dstSize, dstSpan);
        }

        public override void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan)
        {
            //throw new NotSupportedException();
        }

        public override void FillBlock(Int3 offset, Size3 size, uint value)
        {
            if (value != _value)
            {
                //throw new NotSupportedException();
            }
        }

        protected override void Dispose(bool disposing)
        {
        }

        [DoesNotReturn]
        private static void ThrowIndexOutOfRange()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
