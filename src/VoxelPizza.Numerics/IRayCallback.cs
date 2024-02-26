namespace VoxelPizza.Numerics
{
    public interface IRayCallback<T>
    {
        public Int3 Start { get; }
        public Int3 End { get; }

        public bool BreakOnX(ref T state);
        public bool BreakOnY(ref T state);
        public bool BreakOnZ(ref T state);
    }
}
