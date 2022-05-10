using VoxelPizza.Diagnostics;

namespace VoxelPizza.Client
{
    public readonly ref struct UpdateState
    {
        public FrameTime Time { get; }
        public Profiler? Profiler { get; }

        public UpdateState(FrameTime time, Profiler? profiler)
        {
            Time = time;
            Profiler = profiler;
        }
    }
}
