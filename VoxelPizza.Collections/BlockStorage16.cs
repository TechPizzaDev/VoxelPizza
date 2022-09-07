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

        public override uint GetBlock(int x, int y, int z)
        {
            int index = GetIndex(x, y, z);
            ushort value = Unsafe.ReadUnaligned<ushort>(ref _array[index * sizeof(ushort)]);
            return value;
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(destination.Length, Width - x);
            ReadOnlySpan<ushort> u16Src = MemoryMarshal.Cast<byte, ushort>(_array);
            ReadOnlySpan<ushort> src = u16Src.Slice(index, length);
            Span<uint> dst = destination.Slice(0, length);

            Expand16To32(src, dst, length);
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Math.Min(destination.Length, Width * Depth);
            ReadOnlySpan<ushort> u16Src = MemoryMarshal.Cast<byte, ushort>(_array);
            ReadOnlySpan<ushort> src = u16Src.Slice(index, length);
            Span<uint> dst = destination.Slice(0, length);

            Expand16To32(src, dst, length);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            Unsafe.WriteUnaligned(ref _array[index * sizeof(ushort)], (ushort)value);
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(source.Length, Width - x);
            ReadOnlySpan<uint> src = source.Slice(0, length);
            Span<ushort> u16Dst = MemoryMarshal.Cast<byte, ushort>(_array);
            Span<ushort> dst = u16Dst.Slice(index, length);

            for (int i = 0; i < length; i++)
            {
                uint value = src[i];
                dst[i] = (ushort)value;
            }
        }

        public override void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            base.SetBlockLayer(y, source);
        }

        public override void SetBlockRow(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            Span<ushort> u16Dst = MemoryMarshal.Cast<byte, ushort>(_array);
            Span<ushort> dst = u16Dst.Slice(index, length);

            dst.Fill((ushort)value);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<ushort> u16Dst = MemoryMarshal.Cast<byte, ushort>(_array);
            Span<ushort> dst = u16Dst.Slice(index, length);

            dst.Fill((ushort)value);
        }
    }
}
