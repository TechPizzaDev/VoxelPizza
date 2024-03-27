using System.Runtime.CompilerServices;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public readonly unsafe struct CubeIndexGenerator : ICubeIndexGenerator<uint>
    {
        private const int VerticesPerFace = 4;
        private const int IndicesPerFace = 6;

        public uint MaxIndices => IndicesPerFace * 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendQuad(uint* destination, uint vertexOffset)
        {
            uint* ptr = destination;
            ptr[0] = vertexOffset;
            ptr[1] = vertexOffset + 1;
            ptr[2] = vertexOffset + 2;
            ptr[3] = vertexOffset;
            ptr[4] = vertexOffset + 2;
            ptr[5] = vertexOffset + 3;
        }

        public uint AppendLast(uint* destination, ref uint vertexOffset)
        {
            return 0;
        }

        public uint AppendFirst(uint* destination, ref uint vertexOffset)
        {
            return 0;
        }

        public uint AppendBack(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }

        public uint AppendBottom(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }

        public uint AppendFront(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }

        public uint AppendLeft(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }

        public uint AppendRight(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }

        public uint AppendTop(uint* destination, ref uint vertexOffset)
        {
            AppendQuad(destination, vertexOffset);
            vertexOffset += VerticesPerFace;
            return IndicesPerFace;
        }
    }
}
