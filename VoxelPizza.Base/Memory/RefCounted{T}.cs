using System;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Memory
{
    public readonly struct RefCounted<T> : IDisposable
        where T : RefCounted?
    {
        public T Value { get; }

        [MemberNotNullWhen(true, nameof(Value))]
        public bool HasValue => Value != null;

        public RefCounted(T value)
        {
            Value = value;
        }

        public bool TryGetValue([NotNullWhen(true)] out T value)
        {
            value = Value;
            return value != null;
        }

        public void Dispose()
        {
            Value?.DecrementRef();
        }
    }
}
