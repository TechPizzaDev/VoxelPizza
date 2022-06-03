
namespace VoxelPizza.Memory
{
    public interface IRefCounted
    {
        public event RefCountedAction? RefZeroed;

        public int RefCount { get; }

        /// <summary>
        /// Increments the reference count by one.
        /// </summary>
        public int IncrementRef(RefCountType type = RefCountType.Caller);

        /// <summary>
        /// Decrements the reference count by one.
        /// </summary>
        public int DecrementRef(RefCountType type = RefCountType.Caller);
    }
}
