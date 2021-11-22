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

        public uint ByteCapacity { get; private set; }
        public uint ByteCount => (uint)((byte*)_head - (byte*)Buffer);

        public uint Count => ByteCount / (uint)Unsafe.SizeOf<T>();
        public uint Capacity => ByteCapacity / (uint)Unsafe.SizeOf<T>();

        public unsafe Span<T> Span => new(Buffer, (int)Count);
        public unsafe Span<T> FullSpan => new(Buffer, (int)Capacity);

        public ByteStore(HeapPool pool, T* buffer, uint byteCapacity)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            ByteCapacity = byteCapacity;
            Buffer = buffer;
            _head = Buffer;
        }

        public ByteStore(HeapPool pool) : this(pool, null, 0)
        {
        }

        public ByteStore(HeapPool pool, uint capacity) : this(pool, null, 0)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Resize(capacity);
        }

        public ByteStore<T> Clone(HeapPool pool)
        {
            uint byteCount = ByteCount;
            if (byteCount == 0)
                return new ByteStore<T>(pool);

            IntPtr newBuffer = pool.Rent(byteCount, out uint newByteCapacity);
            Unsafe.CopyBlockUnaligned((void*)newBuffer, Buffer, byteCount);

            ByteStore<T> newStore = new(pool, (T*)newBuffer, newByteCapacity);
            newStore._head = (T*)((byte*)newStore._head + byteCount);
            return newStore;
        }

        public ByteStore<T> Clone()
        {
            return Clone(Pool);
        }

        public void MoveByteHead(uint byteCount)
        {
            Debug.Assert((byte*)_head + byteCount <= (byte*)Buffer + ByteCapacity);
            _head = (T*)((byte*)_head + byteCount);
        }

        public void MoveHead(uint count)
        {
            Debug.Assert(_head + count <= Buffer + Capacity);
            _head += count;
        }

        public static ByteStore<T> Create(HeapPool pool, uint capacity)
        {
            IntPtr buffer = pool.Rent(capacity * (uint)Unsafe.SizeOf<T>(), out uint actualByteCapacity);
            return new ByteStore<T>(pool, (T*)buffer, actualByteCapacity);
        }

        private unsafe void Resize(uint newCapacity)
        {
            uint byteCount = ByteCount;
            IntPtr newBuffer = Pool.Rent(newCapacity * (uint)Unsafe.SizeOf<T>(), out uint newByteCapacity);

            IntPtr oldBuffer = (IntPtr)Buffer;
            if (oldBuffer != IntPtr.Zero)
            {
                Unsafe.CopyBlockUnaligned((void*)newBuffer, (void*)oldBuffer, byteCount);
                Pool.Return(ByteCapacity, oldBuffer);
            }

            Buffer = (T*)newBuffer;
            _head = (T*)((byte*)Buffer + byteCount);
            ByteCapacity = newByteCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(uint capacity)
        {
            Debug.Assert(Pool != null);

            if (ByteCapacity < capacity * Unsafe.SizeOf<T>())
            {
                uint newCapacity = Math.Min(capacity * 2, capacity + 1024 * 64);
                Resize(newCapacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareCapacityFor(uint count)
        {
            EnsureCapacity(Count + count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(T value)
        {
            *_head++ = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetAppendSpan(uint count)
        {
            Span<T> slice = new(_head, (int)count);
            _head += count;
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendRange(ReadOnlySpan<T> values)
        {
            values.CopyTo(new(_head, values.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetAppendPtr(uint count)
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
            _head = null;
        }
    }
}
