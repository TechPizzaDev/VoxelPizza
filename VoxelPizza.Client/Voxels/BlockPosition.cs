using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public struct BlockPosition
    {
        public int X;
        public int Y;
        public int Z;

        public BlockPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return x + Chunk.Width * (y + Chunk.Depth * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetIndex(nint x, nint y, nint z)
        {
            return x + Chunk.Width * (y + Chunk.Depth * z);
        }
    }
}
