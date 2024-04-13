using System;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage16 : BlockStorage
    {
        private readonly ushort[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned16;

        public BlockStorage16(int width, int height, int depth) : base(width, depth, height)
        {
            _array = new ushort[(long)height * depth * width];
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
            return _array[index];
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(destination.Length, Width - x);
            ReadOnlySpan<ushort> src = _array.AsSpan(index, length);
            
            Expand16To32(src, destination);
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Math.Min(destination.Length, Width * Depth);
            ReadOnlySpan<ushort> src = _array.AsSpan(index, length);
            
            Expand16To32(src, destination);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            _array[index] = (ushort)value;
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(source.Length, Width - x);
            ReadOnlySpan<uint> src = source.Slice(0, length);
            Span<ushort> dst = _array.AsSpan(index, length);
            
            Narrow(src, dst);
        }

        public override void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<ushort> dst = _array.AsSpan(index, length);
            
            Narrow(source, dst);
        }

        public override void SetBlockRow(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            Span<ushort> dst = _array.AsSpan(index, length);

            dst.Fill((ushort)value);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<ushort> dst = _array.AsSpan(index, length);

            dst.Fill((ushort)value);
        }
    }
}
