using Veldrid;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public abstract class ChunkMeshBase : GraphicsResource
    {
        public abstract int IndexCount { get; }
        public abstract int VertexCount { get; }

        public object WorkerMutex { get; } = new object();

        public abstract bool IsBuildRequired { get; }
        public abstract bool IsUploadRequired { get; }

        public abstract (int Total, int ToBuild, int ToUpload) GetMeshCount();

        public abstract void RequestBuild(ChunkPosition position);

        public abstract bool Build(ChunkMesher mesher, BlockMemory blockMemoryBuffer);

        public abstract bool Upload(
            GraphicsDevice gd, CommandList uploadList, ChunkStagingMeshPool stagingMeshPool,
            out ChunkStagingMesh? stagingMesh);

        public abstract void UploadFinished();

        public abstract void Render(CommandList cl);
    }
}
