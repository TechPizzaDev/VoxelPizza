using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        private Segment[] _segments;

        public HeapPool(int maxCapacity)
        {
            _segments = new Segment[GetSegmentIndex(maxCapacity) + 1];
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i] = new Segment(GetBlockSizeAt(i));
            }
        }

        private static int GetSegmentIndex(int byteCapacity)
        {
            Debug.Assert(byteCapacity >= 0);

            int poolIndex = BitOperations.Log2((uint)byteCapacity - 1 | 0b111111111) - 8;
            return poolIndex;
        }

        private static int GetBlockSizeAt(int index)
        {
            return 1 << (index + 9);
        }

        public int GetBlockSize(int byteCapacity)
        {
            int i = GetSegmentIndex(byteCapacity);
            return 1 << (i + 9);
        }

        public Segment GetSegment(int byteCapacity)
        {
            // Counts between 0 and 512 fit in the zero index.
            int index = GetSegmentIndex(byteCapacity);
            return _segments[index];
        }

        public IntPtr Rent(int byteCapacity, out int actualByteCapacity)
        {
            Segment segment = GetSegment(byteCapacity);
            actualByteCapacity = segment.BlockSize;
            return segment.Rent();
        }

        public void Return(int byteCapacity, IntPtr buffer)
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
