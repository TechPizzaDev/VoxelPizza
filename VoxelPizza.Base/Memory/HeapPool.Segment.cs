using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        public class Segment
        {
            private ConcurrentStack<IntPtr> _pooled = new();

            public int BlockSize { get; }

            public Segment(int blockSize)
            {
                BlockSize = blockSize;
            }

            public IntPtr Rent()
            {
                if (_pooled.TryPop(out IntPtr pooled))
                {
                    return pooled;
                }
                //Console.WriteLine("allocating " + BlockSize);
                return Marshal.AllocHGlobal(BlockSize);
            }

            public void Free(IntPtr buffer)
            {
                if (_pooled.Count < 8)
                {
                    _pooled.Push(buffer);
                    return;
                }
                Console.WriteLine("freeing " + BlockSize);
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
