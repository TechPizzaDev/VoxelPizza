using System;

namespace VoxelPizza.Collections
{
    public sealed class BlockStorage0 : BlockStorage
    {
        public override BlockStorageType StorageType => BlockStorageType.Null;

        public BlockStorage0(ushort width, ushort height, ushort depth) : base(width, height, depth)
        {
            IsEmpty = true;
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = default;
            storageType = StorageType;
            return false;
        }

        public override uint GetBlock(int x, int y, int z)
        {
            int index = GetIndex(x, y, z);

            if (index > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
            return 0;
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            Span<uint> dst = destination.Slice(0, length);

            if (index + length > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
            dst.Clear();
        }

        public override void GetBlockLayer(int y, Span<uint> destination)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            Span<uint> dst = destination.Slice(0, length);

            if (index + dst.Length > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
            dst.Clear();
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);

            if (index > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public override void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            ReadOnlySpan<uint> src = source.Slice(0, length);

            if (index + src.Length > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public override void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            int index = GetIndex(0, y, 0);
            int length = Width * Depth;
            ReadOnlySpan<uint> src = source.Slice(0, length);

            if (index + src.Length > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public override void SetBlockRow(int x, int y, int z, uint value)
        {
            int index = GetIndex(x, y, z);
            int length = Width - x;
            
            if (index + length > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public override void SetBlockLayer(int y, uint value)
        {
            if (y > Height)
            {
                throw new IndexOutOfRangeException();
            }
        }
    }
}
