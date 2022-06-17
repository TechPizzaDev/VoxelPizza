using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage8 : BlockStorage
    {
        private readonly byte[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned8;

        public BlockStorage8(ushort width, ushort height, ushort depth) : base(width, height, depth)
        {
            _array = new byte[(long)height * depth * width * sizeof(byte)];
            IsEmpty = false;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            storageType = BlockStorageType.Unsigned8;
            inlineSpan = _array;
            return true;
        }

        public override void GetBlockRow(int index, Span<uint> destination)
        {
            if (index + destination.Length > _array.Length)
                throw new IndexOutOfRangeException();

            ref byte array = ref MemoryMarshal.GetArrayDataReference(_array);
            ref byte src = ref Unsafe.Add(ref array, index);
            ref byte dst = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(destination));
            Expand8To32(ref src, ref dst, (nuint)destination.Length);
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            GetBlockRow(GetIndex(x, y, z), destination);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            _array.AsSpan(GetIndex(0, y, 0), Width * Depth).Fill((byte)value);
        }

        public override void SetBlock(int index, uint value)
        {
            if (index > _array.Length)
                throw new IndexOutOfRangeException();

            ref byte inline = ref MemoryMarshal.GetArrayDataReference(_array);
            Unsafe.Add(ref inline, index) = (byte)value;
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            SetBlock(GetIndex(x, y, z), value);
        }
    }
}
