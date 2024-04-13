using System;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Collections.Blocks
{
    public sealed class BlockStorage0 : BlockStorage
    {
        private uint _value;

        public override BlockStorageType StorageType => BlockStorageType.Unsigned0;

        public BlockStorage0(int width, int height, int depth) : base(width, height, depth)
        {
            IsEmpty = true;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = default;
            storageType = StorageType;
            return true;
        }

        public override uint GetBlock(int x, int y, int z)
        {
            int index = GetIndex(x, y, z);

            if (index > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
            return _value;
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            Span<uint> dst = destination.Slice(0, length);

            if (index + length > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
            dst.Fill(_value);
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<uint> dst = destination.Slice(0, length);

            if (index + dst.Length > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
            dst.Fill(_value);
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);

            if (index > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            ReadOnlySpan<uint> src = source.Slice(0, length);

            if (index + src.Length > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
        }

        public override void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            ReadOnlySpan<uint> src = source.Slice(0, length);

            if (index + src.Length > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
        }

        public override void SetBlockRow(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;

            if (index + length > Width * Height * Depth)
            {
                ThrowIndexOutOfRange();
            }
        }

        public override void SetBlockLayer(int y, uint value)
        {
            if (y > Height)
            {
                ThrowIndexOutOfRange();
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
