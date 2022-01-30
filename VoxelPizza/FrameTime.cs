using System;

namespace VoxelPizza
{
    public readonly struct FrameTime
    {
        private const double SecondsPerTick = 1.0 / TimeSpan.TicksPerSecond;

        public TimeSpan Total { get; }
        public TimeSpan Delta { get; }
        public bool IsActive { get; }

        public float DeltaSeconds => (float)(Delta.Ticks * SecondsPerTick);
        public float TotalSeconds => (float)(Total.Ticks * SecondsPerTick);

        public FrameTime(TimeSpan total, TimeSpan delta, bool isActive)
        {
            Total = total;
            Delta = delta;
            IsActive = isActive;
        }
    }
}

