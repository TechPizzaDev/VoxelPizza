using System;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Memory
{
    /// <summary>
    /// Represents a tracker used for safely accessing a class deriving from <see cref="RefCounted"/>.
    /// </summary>
    /// <typeparam name="T">The type deriving from <see cref="RefCounted"/> to track.</typeparam>
    public readonly struct RefCounted<T> : IRefCounted, IDisposable
        where T : RefCounted?
    {
        private readonly T _value;

        public event RefCountedAction? RefZeroed
        {
            add => _value!.RefZeroed += value;
            remove => _value!.RefZeroed -= value;
        }

        public T Value
        {
            get
            {
                if (_value == null || _value.RefCount <= 0)
                    throw new InvalidOperationException();
                return _value;
            }
        }

        [MemberNotNullWhen(true, nameof(Value))]
        public bool HasValue => _value != null && _value.RefCount > 0;

        public int RefCount => _value!.RefCount;

        public RefCounted(T value)
        {
            _value = value;
        }

        public bool TryGetValue([NotNullWhen(true)] out T value)
        {
            value = _value;
            return value != null && value.RefCount > 0;
        }

        /// <summary>
        /// Decrements the reference count by one.
        /// </summary>
        public void Dispose()
        {
            _value?.DecrementRef();
        }

        public int IncrementRef(RefCountType type = RefCountType.Caller)
        {
            int count = _value!.IncrementRef(type);
            return count;
        }

        public int DecrementRef(RefCountType type = RefCountType.Caller)
        {
            int count = _value!.DecrementRef(type);
            return count;
        }
    }
}
