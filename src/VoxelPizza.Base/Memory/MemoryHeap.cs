using System;
using System.Runtime.InteropServices;

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
                Copy(
                    buffer,
                    newBuffer,
                    (uint)Math.Min(requestedByteCapacity, previousByteCapacity));

                Free(previousByteCapacity, buffer);
            }
            return newBuffer;
        }

        public virtual void Copy(void* source, void* destination, nuint byteCount)
        {
            NativeMemory.Copy(source, destination, byteCount);
        }
    }
}
