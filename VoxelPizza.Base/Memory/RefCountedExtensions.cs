
namespace VoxelPizza.Memory
{
    public static class RefCountedExtensions
    {
        public static RefCounted<T> TrackRef<T>(this T source, RefCountType type = RefCountType.Caller)
            where T : RefCounted?
        {
            if (source != null)
            {
                int count = source.IncrementRef(type);
                if (count > 0)
                    return new RefCounted<T>(source);
            }
            return new RefCounted<T>(null!);
        }
    }
}
