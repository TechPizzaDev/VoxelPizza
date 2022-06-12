
namespace VoxelPizza.Numerics
{
    public readonly struct StartEndVoxelRayCallback : IRayCallback<VoxelRayCast>
    {
        public int StartX { get; }
        public int StartY { get; }
        public int StartZ { get; }
        public int EndX { get; }
        public int EndY { get; }
        public int EndZ { get; }

        public StartEndVoxelRayCallback(Int3 start, Int3 end)
        {
            StartX = start.X;
            StartY = start.Y;
            StartZ = start.Z;
            EndX = end.X;
            EndY = end.Y;
            EndZ = end.Z;
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
