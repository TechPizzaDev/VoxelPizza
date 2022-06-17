
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public abstract class FaceDependentMeshProvider : MeshProvider
    {
        public override void GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            GenerateFull(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override void GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            GenerateSpace(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override void GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            GenerateSpace(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override void GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            GeneratePaint(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override void GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            GenerateSpacePaint(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public abstract void GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract void GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract void GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract void GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract void GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);
    }
}