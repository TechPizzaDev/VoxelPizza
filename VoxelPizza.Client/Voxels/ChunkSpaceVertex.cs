using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public struct ChunkSpaceVertex
    {
        public float X;
        public float Y;
        public float Z;
        public uint Normal;

        public ChunkSpaceVertex(float x, float y, float z, uint normal)
        {
            X = x;
            Y = y;
            Z = z;
            Normal = normal;
        }

        public ChunkSpaceVertex(Vector3 position, Vector3 normal) :
            this(position.X, position.Y, position.Z, PackNormal(normal))
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
