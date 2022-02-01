using System;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage0 : BlockStorage
    {
        public override BlockStorageType StorageType => BlockStorageType.Null;

        public BlockStorage0(ushort width, ushort height, ushort depth) : base(width, height, depth)
        {
            IsEmpty = true;
        }

        public override void GetBlockRow(nuint index, ref uint destination, nuint length)
        {
            Unsafe.InitBlockUnaligned(ref Unsafe.As<uint, byte>(ref destination), 0, (uint)length * sizeof(uint));
        }

        public override void GetBlockRow(nuint x, nuint y, nuint z, ref uint destination, nuint length)
        {
            GetBlockRow(0, ref destination, length);
        }

        public override void SetBlock(nuint index, uint value)
        {
        }

        public override void SetBlock(nuint x, nuint y, nuint z, uint value)
        {
        }

        public override void SetBlockLayer(nuint y, uint value)
        {
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = default;
            storageType = StorageType;
            return false;
        }
    }
}
