using System;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage8 : BlockStorage
    {
        private readonly byte[] _array;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned8;

        public BlockStorage8(int width, int height, int depth) : base(width, height, depth)
        {
            _array = new byte[(long)height * depth * width];
            IsEmpty = false;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = _array;
            storageType = StorageType;
            return true;
        }

        public override uint GetBlock(int x, int y, int z)
        {
            int index = GetIndex(x, y, z);
            byte value = _array[index];
            return value;
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(destination.Length, Width - x);
            ReadOnlySpan<byte> src = _array.AsSpan(index, length);

            Expand8To32(src, destination);
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Math.Min(destination.Length, Width * Depth);
            ReadOnlySpan<byte> src = _array.AsSpan(index, length);
            
            Expand8To32(src, destination);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            _array[index] = (byte)value;
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Math.Min(source.Length, Width - x);
            ReadOnlySpan<uint> src = source.Slice(0, length);
            Span<byte> dst = _array.AsSpan(index, length);

            for (int i = 0; i < length; i++)
            {
                uint value = src[i];
                dst[i] = (byte)value;
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
            Span<byte> dst = _array.AsSpan(index, length);

            dst.Fill((byte)value);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<byte> dst = _array.AsSpan(index, length);

            dst.Fill((byte)value);
        }
    }
}
