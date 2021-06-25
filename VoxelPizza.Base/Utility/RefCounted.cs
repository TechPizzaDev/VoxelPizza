using System.Threading;

namespace VoxelPizza
{
    public abstract class RefCounted
    {
        private int _refCount;

        public int RefCount => _refCount;

        public int IncrementRef()
        {
            int count = Interlocked.Increment(ref _refCount);
            return count;
        }

        public int DecrementRef()
        {
            int count = Interlocked.Decrement(ref _refCount);
            return count;
        }
    }
}
