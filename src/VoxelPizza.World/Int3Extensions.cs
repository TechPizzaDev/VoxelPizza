using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public static class Int3Extensions
    {
        public static Int3 ToInt3(this BlockPosition position)
        {
            return Unsafe.BitCast<BlockPosition, Int3>(position);
        }

        public static Int3 ToInt3(this ChunkPosition position)
        {
            return Unsafe.BitCast<ChunkPosition, Int3>(position);
        }

        public static Int3 ToInt3(this ChunkRegionPosition position)
        {
            return Unsafe.BitCast<ChunkRegionPosition, Int3>(position);
        }
    }
}
