using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public static class Int3Extensions
    {
        public static Int3 ToInt3(this BlockPosition position)
        {
            return new Int3(position.X, position.Y, position.Z);
        }

        public static Int3 ToInt3(this ChunkPosition position)
        {
            return new Int3(position.X, position.Y, position.Z);
        }

        public static Int3 ToInt3(this ChunkRegionPosition position)
        {
            return new Int3(position.X, position.Y, position.Z);
        }
    }
}
