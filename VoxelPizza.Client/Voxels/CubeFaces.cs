using System;

namespace VoxelPizza.Client
{
    [Flags]
    public enum CubeFaces : byte
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Front = 1 << 4,
        Back = 1 << 5,

        All = Top | Bottom | Left | Right | Front | Back
    }
}
