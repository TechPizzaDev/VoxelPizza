using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage16 : BlockStorage
    {
        private readonly byte[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned16;

        public BlockStorage16(ushort width, ushort height, ushort depth) : base(width, depth, height)
        {
            _array = new byte[(long)height * depth * width * sizeof(ushort)];
            IsEmpty = false;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            storageType = BlockStorageType.Unsigned16;
            inlineSpan = _array;
            return true;
        }

        public override void GetBlockRow(int index, Span<uint> destination)
        {
            if ((index + destination.Length) * sizeof(ushort) > _array.Length)
                throw new IndexOutOfRangeException();

            ref byte array = ref MemoryMarshal.GetArrayDataReference(_array);
            ref byte src = ref Unsafe.Add(ref array, index * sizeof(ushort));
            ref byte dst = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(destination));
            Expand16To32(ref src, ref dst, (uint)destination.Length);
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            GetBlockRow(GetIndex(x, y, z), destination);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            Span<byte> span = _array.AsSpan(
                GetIndex(0, y, 0) * sizeof(ushort),
                Width * Depth * sizeof(ushort));
            MemoryMarshal.Cast<byte, ushort>(span).Fill((ushort)value);
        }

        public override void SetBlock(int index, uint value)
        {
            if (index * sizeof(ushort) > _array.Length)
                throw new IndexOutOfRangeException();

            ref byte array = ref MemoryMarshal.GetArrayDataReference(_array);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref array, index * sizeof(ushort)), (ushort)value);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            SetBlock(GetIndex(x, y, z), value);
        }
    }
}
