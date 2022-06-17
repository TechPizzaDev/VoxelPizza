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

        public override void GetBlockRow(int index, Span<uint> destination)
        {
            if (index + destination.Length > Width * Height * Depth)
                throw new IndexOutOfRangeException();

            destination.Clear();
        }

        public override void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            GetBlockRow(0, destination);
        }

        public override void SetBlock(int index, uint value)
        {
            if (index > Width * Height * Depth)
                throw new IndexOutOfRangeException();
        }

        public override void SetBlock(int x, int y, int z, uint value)
        {
            SetBlock(GetIndex(x, y, z), value);
        }

        public override void SetBlockLayer(int y, uint value)
        {
            if (y > Height)
                throw new IndexOutOfRangeException();
        }

        public override bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            inlineSpan = default;
            storageType = StorageType;
            return false;
        }
    }
}
