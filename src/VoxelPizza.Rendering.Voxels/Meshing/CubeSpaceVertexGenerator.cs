using System.Numerics;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public readonly unsafe struct CubeSpaceVertexGenerator : ICubeVertexGenerator<ChunkSpaceVertex>
    {
        private const int VerticesPerFace = 4;

        public static uint BackNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitZ);
        public static uint BottomNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitY);
        public static uint FrontNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitZ);
        public static uint LeftNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitX);
        public static uint RightNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitX);
        public static uint TopNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitY);

        public readonly float X0;
        public readonly float Y0;
        public readonly float Z0;
        public readonly float X1;
        public readonly float Y1;
        public readonly float Z1;
        
        public uint MaxVertices => VerticesPerFace * 6;

        public CubeSpaceVertexGenerator(float x, float y, float z)
        {
            X0 = x;
            Y0 = y;
            Z0 = z;
            X1 = x + 1;
            Y1 = y + 1;
            Z1 = z + 1;
        }

        public uint AppendFirst(ChunkSpaceVertex* destination)
        {
            return 0;
        }

        public uint AppendLast(ChunkSpaceVertex* destination)
        {
            return 0;
        }

        public uint AppendBack(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = BackNormal;
            float z0 = Z0;
            ptr[0] = new ChunkSpaceVertex(X1, Y1, z0, normal);
            ptr[1] = new ChunkSpaceVertex(X0, Y1, z0, normal);
            ptr[2] = new ChunkSpaceVertex(X0, Y0, z0, normal);
            ptr[3] = new ChunkSpaceVertex(X1, Y0, z0, normal);
            return VerticesPerFace;
        }

        public uint AppendBottom(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = BottomNormal;
            float y0 = Y0;
            ptr[0] = new ChunkSpaceVertex(X0, y0, Z1, normal);
            ptr[1] = new ChunkSpaceVertex(X1, y0, Z1, normal);
            ptr[2] = new ChunkSpaceVertex(X1, y0, Z0, normal);
            ptr[3] = new ChunkSpaceVertex(X0, y0, Z0, normal);
            return VerticesPerFace;
        }

        public uint AppendFront(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = FrontNormal;
            float z1 = Z1;
            ptr[0] = new ChunkSpaceVertex(X0, Y1, z1, normal);
            ptr[1] = new ChunkSpaceVertex(X1, Y1, z1, normal);
            ptr[2] = new ChunkSpaceVertex(X1, Y0, z1, normal);
            ptr[3] = new ChunkSpaceVertex(X0, Y0, z1, normal);
            return VerticesPerFace;
        }

        public uint AppendLeft(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = LeftNormal;
            float x0 = X0;
            ptr[0] = new ChunkSpaceVertex(x0, Y1, Z0, normal);
            ptr[1] = new ChunkSpaceVertex(x0, Y1, Z1, normal);
            ptr[2] = new ChunkSpaceVertex(x0, Y0, Z1, normal);
            ptr[3] = new ChunkSpaceVertex(x0, Y0, Z0, normal);
            return VerticesPerFace;
        }

        public uint AppendRight(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = RightNormal;
            float x1 = X1;
            ptr[0] = new ChunkSpaceVertex(x1, Y1, Z1, normal);
            ptr[1] = new ChunkSpaceVertex(x1, Y1, Z0, normal);
            ptr[2] = new ChunkSpaceVertex(x1, Y0, Z0, normal);
            ptr[3] = new ChunkSpaceVertex(x1, Y0, Z1, normal);
            return VerticesPerFace;
        }

        public uint AppendTop(ChunkSpaceVertex* destination)
        {
            ChunkSpaceVertex* ptr = destination;
            uint normal = TopNormal;
            float y1 = Y1;
            ptr[0] = new ChunkSpaceVertex(X0, y1, Z0, normal);
            ptr[1] = new ChunkSpaceVertex(X1, y1, Z0, normal);
            ptr[2] = new ChunkSpaceVertex(X1, y1, Z1, normal);
            ptr[3] = new ChunkSpaceVertex(X0, y1, Z1, normal);
            return VerticesPerFace;
        }
    }
}
