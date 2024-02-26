namespace VoxelPizza.Numerics
{
    public readonly struct StartEndVoxelRayCallback : IRayCallback<VoxelRayCast>
    {
        public Int3 Start { get; }
        public Int3 End { get; }

        public StartEndVoxelRayCallback(Int3 start, Int3 end)
        {
            Start = start;
            End = end;
        }

        public bool BreakOnX(ref VoxelRayCast state)
        {
            return false;
        }

        public bool BreakOnY(ref VoxelRayCast state)
        {
            return false;
        }

        public bool BreakOnZ(ref VoxelRayCast state)
        {
            return false;
        }
    }
}
