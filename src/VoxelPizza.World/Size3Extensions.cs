using VoxelPizza.Numerics;

namespace VoxelPizza.World;

public static class Size3Extensions
{
    public static Size3 ToSize3(this BlockPosition position)
    {
        return new((uint)position.X, (uint)position.Y, (uint)position.Z);
    }

    public static Size3 ToSize3(this ChunkPosition position)
    {
        return new((uint)position.X, (uint)position.Y, (uint)position.Z);
    }

    public static Size3 ToSize3(this ChunkRegionPosition position)
    {
        return new((uint)position.X, (uint)position.Y, (uint)position.Z);
    }
}
