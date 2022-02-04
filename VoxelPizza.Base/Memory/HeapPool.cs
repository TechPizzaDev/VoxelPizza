using System;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public unsafe partial class HeapPool : MemoryHeap
    {
        private Segment[] _segments;

        // Capacities between 1 and 2^MinRangeBits bytes fit in the first segment.
        private const int MinRangeBits = 8;

        private const uint MinMask = ~0u >> (32 - MinRangeBits);

        private const nuint StepSize = 1024;

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

        public MemoryHeap Heap { get; }
        public uint MaxCapacity { get; }

        public HeapPool(MemoryHeap heap, uint maxCapacity)
        {
            Heap = heap ?? throw new ArgumentNullException(nameof(heap));
            MaxCapacity = maxCapacity;

            _segments = new Segment[GetSegmentIndex(MaxCapacity)];
            for (int i = 0; i < _segments.Length; i++)
            {
                nuint blockSize = GetBlockSizeAt((nuint)i);
                //uint maxCount = Math.Max(4u, 1024u >> Math.Max(0, i - 6));
                uint maxCount = Math.Max(1024 / (uint)(i + 1), 4);

                _segments[i] = new Segment(blockSize, maxCount);
            }
        }

        private static nuint GetSegmentIndex(nuint byteCapacity)
        {
            //int poolIndex = BitOperations.Log2(byteCapacity - 1 | MinMask) - (MinRangeBits - 1);
            nuint poolIndex = (byteCapacity - 1) / StepSize;
            return (nuint)poolIndex;
        }

        private static nuint GetBlockSizeAt(nuint index)
        {
            return (index + 1) * StepSize;
            //return 1u << ((int)index + MinRangeBits);
        }

        public override nuint GetBlockSize(nuint byteCapacity)
        {
            nuint i = GetSegmentIndex(byteCapacity);
            if (i >= (nuint)_segments.Length)
            {
                return Heap.GetBlockSize(byteCapacity);
            }
            return GetBlockSizeAt(i);
        }

        public Segment? GetSegment(nuint byteCapacity)
        {
            nuint index = GetSegmentIndex(byteCapacity);
            Segment[] segments = _segments;
            if (index >= (nuint)segments.Length)
            {
                return null;
            }
            return segments[index];
        }

        public override void* Alloc(nuint byteCapacity, out nuint actualByteCapacity)
        {
            Segment? segment = GetSegment(byteCapacity);
            if (segment == null)
            {
                actualByteCapacity = byteCapacity;
                return Alloc(byteCapacity);
            }
            actualByteCapacity = segment.BlockSize;
            return segment.Rent();
        }

        public override void Free(nuint byteCapacity, void* buffer)
        {
            Segment? segment = GetSegment(byteCapacity);
            if (segment == null)
            {
                Free(buffer);
                return;
            }

            if (segment.BlockSize != byteCapacity)
            {
                throw new InvalidOperationException();
            }
            segment.Return(buffer);
        }

        public override void* Realloc(
            void* buffer,
            nuint previousByteCapacity,
            nuint requestedByteCapacity,
            out nuint actualByteCapacity)
        {
            if (requestedByteCapacity > MaxCapacity)
            {
                actualByteCapacity = requestedByteCapacity;
                return Realloc(buffer, requestedByteCapacity);
            }

            return base.Realloc(
                buffer, 
                previousByteCapacity, 
                requestedByteCapacity, 
                out actualByteCapacity);
        }

        internal static void* Alloc(nuint byteCount)
        {
            if (byteCount == 0)
            {
                return null;
            }
            return NativeMemory.Alloc(byteCount);
        }

        internal static void Free(void* buffer)
        {
            NativeMemory.Free(buffer);
        }

        internal static void* Realloc(void* buffer, nuint byteCount)
        {
            if (byteCount == 0)
            {
                Free(buffer);
                return null;
            }
            return NativeMemory.Realloc(buffer, byteCount);
        }
    }
}
