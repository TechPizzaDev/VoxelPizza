
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public abstract class FaceDependentMeshProvider : MeshProvider
    {
        public override bool GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            return GenerateFull(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override bool GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            return GenerateSpace(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override bool GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            return GenerateSpace(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override bool GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            return GeneratePaint(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public override bool GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            return GenerateSpacePaint(ref meshOutput, ref mesherState, CubeFaces.All);
        }

        public abstract bool GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract bool GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract bool GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract bool GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);

        public abstract bool GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces);
    }
}