using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        public unsafe class Segment
        {
            private Stack<IntPtr> _pooled = new();

            public nuint BlockSize { get; }
            public uint MaxCount { get; }

            public uint Count => (uint)_pooled.Count;

            public Segment(nuint blockSize, uint maxCount)
            {
                if (blockSize > long.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(blockSize));

                BlockSize = blockSize;
                MaxCount = maxCount;
            }

            public void* Rent(MemoryHeap heap, out nuint actualByteCapacity)
            {
                lock (_pooled)
                {
                    if (_pooled.TryPop(out IntPtr pooled))
                    {
                        actualByteCapacity = BlockSize;
                        return (void*)pooled;
                    }
                }

                return heap.Alloc(BlockSize, out actualByteCapacity);
            }

            public void Return(MemoryHeap heap, void* buffer)
            {
                Debug.Assert(buffer != null);

                lock (_pooled)
                {
                    if ((uint)_pooled.Count < MaxCount)
                    {
                        _pooled.Push((IntPtr)buffer);
                        return;
                    }
                }

                heap.Free(BlockSize, buffer);
            }
        }
    }
}
