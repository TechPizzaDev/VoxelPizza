using System;

namespace VoxelPizza.Client
{
    [Flags]
    public enum ChunkGraphFaces : ushort
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Front = 1 << 4,
        Back = 1 << 5,
        Center = 1 << 6,
        
        Empty = 1 << 7,
        Update = 1 << 8,
        
        AllSides = Top | Bottom | Left | Right | Front | Back,
        All = AllSides | Center,
    }
}
