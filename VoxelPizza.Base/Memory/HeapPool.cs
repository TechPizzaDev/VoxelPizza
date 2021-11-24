using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        private Segment[] _segments;

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

        public HeapPool(uint maxCapacity)
        {
            _segments = new Segment[GetSegmentIndex(maxCapacity) + 1];
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i] = new Segment(GetBlockSizeAt((uint)i));
            }
        }

        private static uint GetSegmentIndex(uint byteCapacity)
        {
            Debug.Assert(byteCapacity >= 0);

            uint poolIndex = (uint)BitOperations.Log2(byteCapacity - 1 | 0b111111111) - 8;
            return poolIndex;
        }

        private static uint GetBlockSizeAt(uint index)
        {
            return 1u << ((int)index + 9);
        }

        public uint GetBlockSize(uint byteCapacity)
        {
            uint i = GetSegmentIndex(byteCapacity);
            return GetBlockSizeAt(i);
        }

        public Segment GetSegment(uint byteCapacity)
        {
            // Counts between 0 and 512 fit in the zero index.
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
