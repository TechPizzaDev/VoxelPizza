using System.Diagnostics;
using System.Threading;

namespace VoxelPizza.Memory
{
    public delegate void RefCountedAction(RefCounted instance);

    public abstract class RefCounted : IRefCounted
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

        ~RefCounted()
        {
            if (_refCount != 0)
            {
                LeakAtFinalizer();
            }
        }

        protected virtual void LeakAtFinalizer()
        {
        }
    }
}
