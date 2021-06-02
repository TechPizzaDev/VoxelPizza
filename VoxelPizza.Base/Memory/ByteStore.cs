using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace VoxelPizza
{
    public unsafe struct ByteStore<T>
        where T : unmanaged
    {
        private T* _head;

        public HeapPool Pool { get; }
        public T* Buffer { get; private set; }
        public T* Head => _head;

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

        public ByteStore(HeapPool pool) : this(pool, null, 0)
        {
        }

        public ByteStore(HeapPool pool, int capacity) : this(pool, null, 0)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Resize(capacity);
        }

        public ByteStore<T> Clone(HeapPool pool)
        {
            int byteCount = ByteCount;
            if (byteCount == 0)
                return new ByteStore<T>(pool);

            IntPtr newBuffer = pool.Rent(byteCount, out int newByteCapacity);
            Unsafe.CopyBlockUnaligned((void*)newBuffer, Buffer, (uint)byteCount);

            ByteStore<T> newStore = new(pool, (T*)newBuffer, newByteCapacity);
            newStore._head = (T*)((byte*)newStore._head + byteCount);
            return newStore;
        }

        public ByteStore<T> Clone()
        {
            return Clone(Pool);
        }

        public void MoveByteHead(int byteCount)
        {
            Debug.Assert((byte*)_head + byteCount <= (byte*)Buffer + ByteCapacity);
            _head = (T*)((byte*)_head + byteCount);
        }

        public void MoveHead(int count)
        {
            Debug.Assert(_head + count <= Buffer + Capacity);
            _head += count;
        }

        public static ByteStore<T> Create(HeapPool pool, int capacity)
        {
            IntPtr buffer = pool.Rent(capacity * Unsafe.SizeOf<T>(), out int actualByteCapacity);
            return new ByteStore<T>(pool, (T*)buffer, actualByteCapacity);
        }

        private unsafe void Resize(int newCapacity)
        {
            int byteCount = ByteCount;
            IntPtr newBuffer = Pool.Rent(newCapacity * Unsafe.SizeOf<T>(), out int newByteCapacity);

            IntPtr oldBuffer = (IntPtr)Buffer;
            if (oldBuffer != IntPtr.Zero)
            {
                Unsafe.CopyBlockUnaligned((void*)newBuffer, (void*)oldBuffer, (uint)byteCount);
                Pool.Return(ByteCapacity, oldBuffer);
            }

            Buffer = (T*)newBuffer;
            _head = (T*)((byte*)Buffer + byteCount);
            ByteCapacity = newByteCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            Debug.Assert(Pool != null);

            if (ByteCapacity < capacity * Unsafe.SizeOf<T>())
            {
                int newCapacity = Math.Min(capacity * 2, capacity + 1024 * 64);
                Resize(newCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareCapacityFor(int count)
        {
            EnsureCapacity(Count + count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(T value)
        {
            *_head++ = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetAppendSpan(int count)
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
        public T* GetAppendPtr(int count)
        {
            T* r = _head;
            _head += count;
            return r;
        }

        public void Clear()
        {
            _head = Buffer;
        }

        public void Dispose()
        {
            IntPtr buffer = (IntPtr)Buffer;
            if (buffer != IntPtr.Zero)
                Pool.Return(ByteCapacity, buffer);
            Buffer = null;
        }
    }
}
