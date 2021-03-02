using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public struct ByteStorage<T> : IDisposable
        where T : unmanaged
    {
        public ArrayPool<byte> ArrayPool { get; }
        public byte[]? Buffer { get; private set; }
        public int Count { get; }

        public Span<T> Span => MemoryMarshal.Cast<byte, T>(Buffer).Slice(0, Count);

        public ByteStorage(ArrayPool<byte> arrayPool, byte[]? buffer, int count)
        {
            ArrayPool = arrayPool ?? throw new ArgumentNullException(nameof(arrayPool));
            Buffer = buffer;
            Count = count;
        }

        public ByteStorage(ByteStore<T> store) : this(store.ArrayPool, store.Buffer, store.Count)
        {
        }

        public void Dispose()
        {
            if (Buffer != null)
            {
                ArrayPool.Return(Buffer);
                Buffer = null;
            }
        }
    }
}
