using System;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Memory
{
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
                if (_value == null)
                    throw new InvalidOperationException();
                return _value;
            }
        }

        [MemberNotNullWhen(true, nameof(Value))]
        public bool HasValue => _value != null;

        public int RefCount => _value!.RefCount;

        public RefCounted(T value)
        {
            _value = value;
        }

        public bool TryGetValue([NotNullWhen(true)] out T value)
        {
            value = _value;
            return value != null;
        }

        public T GetValueOrDefault()
        {
            return _value;
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
