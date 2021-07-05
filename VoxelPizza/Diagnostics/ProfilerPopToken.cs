
namespace VoxelPizza.Diagnostics
{
    public ref struct ProfilerPopToken
    {
        public Profiler? Profiler { get; }

        public ProfilerPopToken(Profiler? profiler)
        {
            Profiler = profiler;
        }

        public void Dispose()
        {
            if (Profiler != null)
            {
                Profiler.Pop();
            }
        }
    }
}
