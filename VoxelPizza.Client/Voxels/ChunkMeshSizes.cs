using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public readonly struct ChunkMeshSizes
    {
        public uint IndexCount { get; }
        public uint IndirectBytesRequired { get; }
        public uint RenderInfoBytesRequired { get; }
        public uint IndexBytesRequired { get; }

        public uint SpaceVertexBytesRequired { get; }
        public uint PaintVertexBytesRequired { get; }

        public bool IsEmpty => IndexCount == 0;
        public uint DrawCount => RenderInfoBytesRequired / (uint)Unsafe.SizeOf<ChunkRenderInfo>();
        public uint VertexCount => SpaceVertexBytesRequired / (uint)Unsafe.SizeOf<ChunkSpaceVertex>();

        public uint TotalBytesRequired =>
            IndirectBytesRequired +
            RenderInfoBytesRequired +
            IndexBytesRequired +
            SpaceVertexBytesRequired +
            PaintVertexBytesRequired;

        public ChunkMeshSizes(
            uint indexCount,
            uint indirectBytesRequired,
            uint renderInfoBytesRequired,
            uint indexBytesRequired,
            uint spaceVertexBytesRequired,
            uint paintVertexBytesRequired)
        {
            IndexCount = indexCount;
            IndirectBytesRequired = indirectBytesRequired;
            RenderInfoBytesRequired = renderInfoBytesRequired;
            IndexBytesRequired = indexBytesRequired;
            SpaceVertexBytesRequired = spaceVertexBytesRequired;
            PaintVertexBytesRequired = paintVertexBytesRequired;
        }
    }

}
