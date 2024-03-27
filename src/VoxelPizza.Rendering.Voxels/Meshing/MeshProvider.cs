
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public abstract class MeshProvider
    {
        /// <returns>
        /// <see langword="true"/> when memory allocation succeeded; 
        /// <see langword="false"/> otherwise.
        /// </returns>
        public abstract bool GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract bool GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract bool GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract bool GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);

        public abstract bool GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherData);
    }
}