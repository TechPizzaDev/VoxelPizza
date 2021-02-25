using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public struct ChunkSpaceVertex
    {
        public Vector3 Position;
        public uint Normal;

        public ChunkSpaceVertex(Vector3 position, uint normal)
        {
            Position = position;
            Normal = normal;
        }

        public ChunkSpaceVertex(Vector4 position, uint normal)
        {
            Position = Unsafe.As<Vector4, Vector3>(ref position);
            Normal = normal;
        }

        public ChunkSpaceVertex(Vector3 position, Vector3 normal) : this(position, PackNormal(normal))
        {
        }

        public ChunkSpaceVertex(Vector4 position, Vector4 normal) : this(position, PackNormal(normal))
        {
        }

        public static uint Pack(uint x, uint y, uint z)
        {
            return x | (y << 10) | (z << 20);
        }

        public static uint Pack(Vector3 vector)
        {
            uint nx = (uint)vector.X;
            uint ny = (uint)vector.Y;
            uint nz = (uint)vector.Z;
            return Pack(nx, ny, nz);
        }

        public static uint PackNormal(Vector4 normal)
        {
            normal += Vector4.One;
            normal *= 1023f / 2f;
            return Pack(Unsafe.As<Vector4, Vector3>(ref normal));
        }

        public static uint PackNormal(Vector3 normal)
        {
            normal += Vector3.One;
            normal *= 1023f / 2f;
            return Pack(normal);
        }
    }
}
