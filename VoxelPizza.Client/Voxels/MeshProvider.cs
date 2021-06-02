
namespace VoxelPizza.Client
{
    public abstract class MeshProvider
    {
        public abstract void GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract void GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract void GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract void GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract void GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);
    }
}