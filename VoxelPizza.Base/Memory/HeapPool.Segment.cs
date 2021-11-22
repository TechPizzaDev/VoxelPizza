using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public partial class HeapPool
    {
        public class Segment
        {
            private Stack<IntPtr> _pooled = new();

            public uint BlockSize { get; }

            public uint Count => (uint)_pooled.Count;

            public Segment(uint blockSize)
            {
                BlockSize = blockSize;
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
                return Marshal.AllocHGlobal((int)BlockSize);
            }

            public void Free(IntPtr buffer)
            {
                lock (_pooled)
                {
                    if (_pooled.Count < 32)
                    {
                        _pooled.Push(buffer);
                        return;
                    }
                }

                //Console.WriteLine("freeing " + BlockSize);
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
