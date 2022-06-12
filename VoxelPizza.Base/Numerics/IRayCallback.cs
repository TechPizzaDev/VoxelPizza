namespace VoxelPizza.Numerics
{
    public interface IRayCallback<T>
    {
        public int StartX { get; }
        public int StartY { get; }
        public int StartZ { get; }
        public int EndX { get; }
        public int EndY { get; }
        public int EndZ { get; }

        public bool BreakOnX(ref T state);
        public bool BreakOnY(ref T state);
        public bool BreakOnZ(ref T state);
    }
}
