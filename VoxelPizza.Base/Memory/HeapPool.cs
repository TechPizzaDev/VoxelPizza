using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        private Segment[] _segments;

        // Capacities between 1 and 2^MinRangeBits bytes fit in the first segment.
        private const int MinRangeBits = 8;

        private const uint MinMask = ~0u >> (32 - MinRangeBits);

        public ulong AvailableBytes
        {
            get
            {
                ulong total = 0;
                foreach (Segment segment in _segments)
                {
                    total += segment.BlockSize * segment.Count;
                }
                return total;
            }
        }

        public uint MaxCapacity { get; }

        public HeapPool(uint maxCapacity)
        {
            MaxCapacity = maxCapacity;

            _segments = new Segment[GetSegmentIndex(MaxCapacity) + 1];
            for (int i = 0; i < _segments.Length; i++)
            {
                uint blockSize = GetBlockSizeAt((uint)i);
                uint maxCount = Math.Max(4u, 1024u >> Math.Max(0, i - 6));

                _segments[i] = new Segment(blockSize, maxCount);
            }
        }

        private static uint GetSegmentIndex(uint byteCapacity)
        {
            Debug.Assert(byteCapacity > 0);

            uint poolIndex = (uint)BitOperations.Log2(byteCapacity - 1 | MinMask) - (MinRangeBits - 1);
            return poolIndex;
        }

        private static uint GetBlockSizeAt(uint index)
        {
            return 1u << ((int)index + MinRangeBits);
        }

        public uint GetBlockSize(uint byteCapacity)
        {
            uint i = GetSegmentIndex(byteCapacity);
            return GetBlockSizeAt(i);
        }

        public Segment GetSegment(uint byteCapacity)
        {
            uint index = GetSegmentIndex(byteCapacity);
            return _segments[index];
        }

        public IntPtr Rent(uint byteCapacity, out uint actualByteCapacity)
        {
            Segment segment = GetSegment(byteCapacity);
            actualByteCapacity = segment.BlockSize;
            return segment.Rent();
        }

        public void Return(uint byteCapacity, IntPtr buffer)
        {
            if (buffer == IntPtr.Zero)
                return;

            Segment segment = GetSegment(byteCapacity);
            if (segment.BlockSize != byteCapacity)
                throw new InvalidOperationException();

            segment.Free(buffer);
        }
    }
}
