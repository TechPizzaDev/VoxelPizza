using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

            public void* Rent()
            {
                lock (_pooled)
                {
                    if (_pooled.TryPop(out IntPtr pooled))
                    {
                        return (void*)pooled;
                    }
                }

                //Console.WriteLine("allocating " + BlockSize);
                GC.AddMemoryPressure((long)BlockSize);
                return NativeMemory.Alloc(BlockSize);
            }

            public void Return(void* buffer)
            {
                lock (_pooled)
                {
                    if ((uint)_pooled.Count < MaxCount)
                    {
                        _pooled.Push((IntPtr)buffer);
                        return;
                    }
                }

                //Console.WriteLine("freeing " + BlockSize);
                NativeMemory.Free(buffer);
                GC.RemoveMemoryPressure((long)BlockSize);
            }
        }
    }
}
