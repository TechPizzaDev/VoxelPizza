using System;
using System.Runtime.CompilerServices;

namespace VoxelPizza
{
    public unsafe abstract class MemoryHeap
    {
        public abstract nuint GetBlockSize(nuint byteCapacity);

        public abstract void* Alloc(nuint byteCapacity, out nuint actualByteCapacity);

        public abstract void Free(nuint byteCapacity, void* buffer);

        public virtual void* Realloc(
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

            void* newBuffer = Alloc(requestedByteCapacity, out actualByteCapacity);
            if (buffer != null)
            {
                Unsafe.CopyBlockUnaligned(
                    newBuffer,
                    buffer,
                    (uint)Math.Min(requestedByteCapacity, previousByteCapacity));

                Free(previousByteCapacity, buffer);
            }
            return newBuffer;
        }
    }
}
