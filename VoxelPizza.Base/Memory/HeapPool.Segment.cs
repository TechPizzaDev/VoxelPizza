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

            public uint BlockSize { get; }
            public uint MaxCount { get; }

            public uint Count => (uint)_pooled.Count;

            public Segment(uint blockSize, uint maxCount)
            {
                BlockSize = blockSize;
                MaxCount = maxCount;
            }

            public IntPtr Rent()
            {
                lock (_pooled)
                {
                    if (_pooled.TryPop(out IntPtr pooled))
                    {
                        return pooled;
                    }
                }

                //Console.WriteLine("allocating " + BlockSize);
                GC.AddMemoryPressure(BlockSize);
                return (IntPtr)NativeMemory.Alloc(BlockSize);
            }

            public void Free(IntPtr buffer)
            {
                lock (_pooled)
                {
                    if ((uint)_pooled.Count < MaxCount)
                    {
                        _pooled.Push(buffer);
                        return;
                    }
                }

                //Console.WriteLine("freeing " + BlockSize);
                NativeMemory.Free((void*)buffer);
                GC.RemoveMemoryPressure(BlockSize);
            }
        }
    }
}
