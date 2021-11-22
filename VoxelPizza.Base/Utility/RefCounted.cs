using System.Diagnostics;
using System.Threading;

namespace VoxelPizza
{
    public delegate void RefCountedAction(RefCounted instance);

    public abstract class RefCounted
    {
        private int _refCount;

        public event RefCountedAction? RefZeroed;

        public int RefCount => _refCount;

        public int IncrementRef(RefCountType type = RefCountType.Caller)
        {
            int count = Interlocked.Increment(ref _refCount);
            return count;
        }

        public int DecrementRef(RefCountType type = RefCountType.Caller)
        {
            int count = Interlocked.Decrement(ref _refCount);
            Debug.Assert(count >= 0);

            if (count == 0)
            {
                RefZeroed?.Invoke(this);
            }
            return count;
        }
    }

    public enum RefCountType
    {
        Caller,
        Container,
    }
}
