using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public class HeapPool
    {
        public IntPtr Rent(int byteCapacity, out int actualByteCapacity)
        {
            actualByteCapacity = byteCapacity;
            return Marshal.AllocHGlobal(byteCapacity);
        }

        public void Return(IntPtr buffer)
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public unsafe struct ByteStore<T>
        where T : unmanaged
    {
        private T* _head;

        public HeapPool Pool { get; }
        public T* Buffer { get; private set; }

        public int ByteCapacity { get; private set; }
        public int ByteCount => (int)((byte*)_head - (byte*)Buffer);

        public int Count => ByteCount / Unsafe.SizeOf<T>();
        public int Capacity => ByteCapacity / Unsafe.SizeOf<T>();

        public unsafe Span<T> Span => new(Buffer, Count);
        public unsafe Span<T> FullSpan => new(Buffer, Capacity);


        public ByteStore(HeapPool pool, T* buffer, int byteCapacity)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            ByteCapacity = byteCapacity;
            Buffer = buffer;
            _head = Buffer;
        }

        public ByteStore(HeapPool arrayPool) : this(arrayPool, null, 0)
        {
        }

        public static ByteStore<T> Create(HeapPool pool, int capacity)
        {
            IntPtr buffer = pool.Rent(capacity * Unsafe.SizeOf<T>(), out int actualByteCapacity);
            return new ByteStore<T>(pool, (T*)buffer, actualByteCapacity);
        }

        public unsafe void EnsureCapacity(int capacity)
        {
            Debug.Assert(Pool != null);

            if (ByteCapacity < capacity * Unsafe.SizeOf<T>())
            {
                int byteCount = ByteCount;
                IntPtr oldBuffer = (IntPtr)Buffer;
                IntPtr newBuffer = Pool.Rent((capacity + 4096) * Unsafe.SizeOf<T>(), out int newByteCapacity);

                if (oldBuffer != IntPtr.Zero)
                {
                    Span<T> oldSpan = Span;
                    oldSpan.CopyTo(new Span<T>((void*)newBuffer, oldSpan.Length));
                    Pool.Return(oldBuffer);
                }

                Buffer = (T*)newBuffer;
                _head = (T*)((byte*)Buffer + byteCount);
                ByteCapacity = newByteCapacity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareCapacity(int count)
        {
            EnsureCapacity(Count + count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(T value)
        {
            *_head++ = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetAppendRange(int count)
        {
            Span<T> slice = new(_head, count);
            _head += count;
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendRange(ReadOnlySpan<T> values)
        {
            values.CopyTo(new(_head, values.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendRange(T item0, T item1, T item2, T item3)
        {
            _head[0] = item0;
            _head[1] = item1;
            _head[2] = item2;
            _head[3] = item3;
            _head += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendRange(T item0, T item1, T item2, T item3, T item4, T item5)
        {
            _head[0] = item0;
            _head[1] = item1;
            _head[2] = item2;
            _head[3] = item3;
            _head[4] = item4;
            _head[5] = item5;
            _head += 6;
        }

        public void Clear()
        {
            _head = Buffer;
        }

        public void Dispose()
        {
            IntPtr buffer = (IntPtr)Buffer;
            if (buffer != IntPtr.Zero)
                Pool.Return(buffer);
            Buffer = null;
        }
    }
}
