
namespace VoxelPizza.Memory
{
    public static class RefCountedExtensions
    {
        /// <summary>
        /// Increments the reference count and returns a disposable tracker around the object.
        /// </summary>
        /// <remarks>
        /// The tracker will wrap around <see langword="null"/> if 
        /// <paramref name="source"/> is <see langword="null"/> or 
        /// the reference count is zeroed.
        /// </remarks>
        /// <typeparam name="T">The class deriving from <see cref="RefCounted"/> to track.</typeparam>
        /// <param name="source">The object to track.</param>
        /// <param name="type">The type of held reference.</param>
        /// <returns>The tracker used to safely access the tracked object.</returns>
        public static RefCounted<T> TrackRef<T>(this T source, RefCountType type = RefCountType.Caller)
            where T : RefCounted?
        {
            if (source != null)
            {
                int count = source.IncrementRef(type);
                if (count > 0)
                {
                    return new RefCounted<T>(source);
                }
            }
            return new RefCounted<T>(null!);
        }

        /// <summary>
        /// Disposes the reference and replaces the tracked object with <see langword="null"/>.
        /// </summary>
        /// <typeparam name="T">The inner type of the tracker.</typeparam>
        /// <param name="source">The reference to dispose and clear.</param>
        public static void Invalidate<T>(ref this RefCounted<T> source)
            where T : RefCounted?
        {
            source.Dispose();
            source = default;
        }
    }
}
