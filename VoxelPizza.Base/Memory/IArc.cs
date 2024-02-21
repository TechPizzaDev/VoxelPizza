
namespace VoxelPizza.Memory
{
    /// <summary>
    /// Base interface for types that use reference counting.
    /// </summary>
    public interface IRefCounted
    {
        /// <summary>
        /// Invoked when the reference count reaches zero.
        /// </summary>
        public event RefCountedAction? RefZeroed;

        /// <summary>
        /// The current reference count.
        /// </summary>
        public int RefCount { get; }

        /// <summary>
        /// Increments the reference count by one.
        /// </summary>
        /// <param name="type">The type of held reference.</param>
        /// <returns>The reference count after the increment.</returns>
        public int IncrementRef(RefCountType type = RefCountType.Caller);

        /// <summary>
        /// Decrements the reference count by one.
        /// </summary>
        /// <param name="type">The type of held reference.</param>
        /// <returns>The reference count after the decrement.</returns>
        public int DecrementRef(RefCountType type = RefCountType.Caller);
    }
}
