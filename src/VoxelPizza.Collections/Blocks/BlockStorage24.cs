using System;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage24 : BlockStorage
    {
        private readonly UInt24[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned24;

        public BlockStorage24(int width, int height, int depth) : base(width, depth, height)
        {
            _array = new UInt24[(long)height * depth * width];
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
            ReadOnlySpan<UInt24> src = _array.AsSpan(index, length);
            
            Expand24To32(src, destination);
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Math.Min(destination.Length, Width * Depth);
            ReadOnlySpan<UInt24> src = _array.AsSpan(index, length);
            
            Expand24To32(src, destination);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            _array[index] = (UInt24)value;
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(source.Length, Width - x);
            ReadOnlySpan<uint> src = source.Slice(0, length);
            Span<UInt24> dst = _array.AsSpan(index, length);

            for (int i = 0; i < length; i++)
            {
                uint value = src[i];
                dst[i] = (UInt24)value;
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
            Span<UInt24> dst = _array.AsSpan(index, length);

            dst.Fill((UInt24)value);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<UInt24> dst = _array.AsSpan(index, length);

            dst.Fill((UInt24)value);
        }
    }
}
