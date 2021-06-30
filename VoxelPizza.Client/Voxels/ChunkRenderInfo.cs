using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkRenderInfo
    {
        public Vector4 Translation;

        public ChunkRenderInfo(Vector4 translation)
        {
            Translation = translation;
        }
    }
}
