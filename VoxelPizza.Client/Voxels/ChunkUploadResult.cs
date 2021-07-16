using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public readonly struct ChunkUploadResult
    {
        public ChunkStagingMesh? StagingMesh { get; }
        public int IndexCount { get; }
        public int IndirectBytesRequired { get; }
        public int RenderInfoBytesRequired { get; }
        public int IndexBytesRequired { get; }
        public int SpaceVertexBytesRequired { get; }
        public int PaintVertexBytesRequired { get; }

        public bool IsEmpty => IndexCount == 0;
        public int DrawCount => RenderInfoBytesRequired / Unsafe.SizeOf<ChunkRenderInfo>();
        public int VertexCount => SpaceVertexBytesRequired / Unsafe.SizeOf<ChunkSpaceVertex>();

        public ChunkUploadResult(
            ChunkStagingMesh? stagingMesh,
            int indexCount,
            int indirectBytesRequired,
            int renderInfoBytesRequired,
            int indexBytesRequired,
            int spaceVertexBytesRequired,
            int paintVertexBytesRequired)
        {
            StagingMesh = stagingMesh;
            IndexCount = indexCount;
            IndirectBytesRequired = indirectBytesRequired;
            RenderInfoBytesRequired = renderInfoBytesRequired;
            IndexBytesRequired = indexBytesRequired;
            SpaceVertexBytesRequired = spaceVertexBytesRequired;
            PaintVertexBytesRequired = paintVertexBytesRequired;
        }
    }

}
