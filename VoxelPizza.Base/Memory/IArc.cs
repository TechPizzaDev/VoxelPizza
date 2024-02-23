using System;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Memory
{
    /// <summary>
    /// Base interface for types that use reference counting.
    /// </summary>
    public interface IArc<T>
        where T : IDestroyable
    {
        public bool HasTarget { get; }

        /// <summary>
        /// The current reference count.
        /// </summary>
        public nint Count { get; }
        
        public ref T Get();

        public bool TryGet([MaybeNullWhen(false)] out T value);

        /// <summary>
        /// Increments the reference count by one.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Increment();

        /// <summary>
        /// Decrements the reference count by one.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Decrement();
    }
}
