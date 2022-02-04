using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public sealed unsafe class NativeMemoryHeap : MemoryHeap
    {
        public static NativeMemoryHeap Instance { get; } = new();

        private NativeMemoryHeap()
        {
        }

        public override nuint GetBlockSize(nuint byteCapacity)
        {
            return byteCapacity;
        }

        public override void* Alloc(nuint byteCapacity, out nuint actualByteCapacity)
        {
            actualByteCapacity = byteCapacity;
            if (byteCapacity == 0)
            {
                return null;
            }
            return NativeMemory.Alloc(byteCapacity);
        }

        public override void Free(nuint byteCapacity, void* buffer)
        {
            NativeMemory.Free(buffer);
        }

        public override unsafe void* Realloc(
            void* buffer,
            nuint previousByteCapacity,
            nuint requestedByteCapacity,
            out nuint actualByteCapacity)
        {
            if (previousByteCapacity == requestedByteCapacity)
            {
                actualByteCapacity = requestedByteCapacity;
                return buffer;
            }
            
            actualByteCapacity = requestedByteCapacity;
            if (requestedByteCapacity == 0)
            {
                NativeMemory.Free(buffer);
                return null;
            }
            return NativeMemory.Realloc(buffer, requestedByteCapacity);
        }
    }
}
