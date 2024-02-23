using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace VoxelPizza.Memory
{
    /// <summary>
    /// Represents a tracker used for safely accessing an <see cref="Arc{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type deriving from <see cref="IDestroyable"/> to track.</typeparam>
    public struct ValueArc<T> : IArc<T>, IDisposable
        where T : IDestroyable
    {
        private Arc<T>? _value;

        public static ValueArc<T> Empty => new(null);

        public bool HasTarget => _value != null && _value.HasTarget;

        public nint Count => _value!.Count;

        internal ValueArc(Arc<T>? value)
        {
            _value = value;
        }

        public ref T Get()
        {
            return ref _value!.Get();
        }

        public bool TryGet([MaybeNullWhen(false)] out T value)
        {
            if (_value == null)
            {
                value = default;
                return false;
            }
            return _value.TryGet(out value);
        }
        
        public T? TryGet()
        {
            if (_value == null)
            {
                return default;
            }
            return _value.TryGet();
        }

        /// <summary>
        /// Decrements the reference count by one and invalidates this <see cref="ValueArc{T}"/>.
        /// </summary>
        public void Dispose()
        {
            Arc<T>? arc = Interlocked.Exchange(ref _value, null);
            arc?.Decrement();
        }

        /// <inheritdoc/>
        public void Increment()
        {
            _value!.Increment();
        }

        /// <inheritdoc/>
        public void Decrement()
        {
            _value!.Decrement();
        }
    }
}