using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage8 : BlockStorage
    {
        private readonly ushort _width;
        private readonly ushort _height;
        private readonly ushort _depth;
        private readonly byte[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned8;
        public override ushort Width => _width;
        public override ushort Height => _height;
        public override ushort Depth => _depth;
        public override bool IsEmpty => false;

        public BlockStorage8(ushort width, ushort height, ushort depth)
        {
            _width = width;
            _height = height;
            _depth = depth;
            _array = new byte[(long)height * depth * width * sizeof(byte)];
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            storageType = BlockStorageType.Unsigned8;
            if (_array != null)
            {
                inlineSpan = _array;
                return true;
            }

            inlineSpan = default;
            return false;
        }

        public override void GetBlockRow(nuint index, ref uint destination, nuint length)
        {
            ref byte array = ref MemoryMarshal.GetArrayDataReference(_array);
            ref byte array8 = ref Unsafe.Add(ref array, index);
            Expand8To32(ref array8, ref Unsafe.As<uint, byte>(ref destination), length);
        }

        public override void GetBlockRow(nuint x, nuint y, nuint z, ref uint destination, nuint length)
        {
            GetBlockRow(GetIndex(x, y, z), ref destination, length);
        }

        public override void SetBlockLayer(nuint y, uint value)
        {
            _array.AsSpan((int)GetIndex(0, y, 0), Width * Depth).Fill((byte)value);
        }

        public override void SetBlock(nuint index, uint value)
        {
            ref byte inline = ref MemoryMarshal.GetArrayDataReference(_array);
            Unsafe.Add(ref inline, index) = (byte)value;
        }

        public override void SetBlock(nuint x, nuint y, nuint z, uint value)
        {
            SetBlock(GetIndex(x, y, z), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override nuint GetIndex(nuint x, nuint y, nuint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }
    }
}