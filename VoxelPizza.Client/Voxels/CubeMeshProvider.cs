
namespace VoxelPizza.Client
{
    public class CubeMeshProvider : FaceDependentMeshProvider
    {
        // TODO: fix this temporary mess
        public TextureAnimation[] anims;

        public override void GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var indGen = new CubeIndexGenerator();
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(anims[blockId], blockId * 2);
            
            CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateFullFrom(ref meshOutput, faces, ref indGen, ref spaGen, ref paiGen);
        }

        public override void GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var indGen = new CubeIndexGenerator();
            
            CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateIndicesFrom(ref meshOutput, faces, ref indGen);
        }

        public override void GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateSpaceFrom(ref meshOutput, faces, ref spaGen);
        }

        public override void GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(anims[blockId], blockId * 2);

            CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GeneratePaintFrom(ref meshOutput, faces, ref paiGen);
        }

        public override void GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(anims[blockId], blockId * 2);

            CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateSpacePaintFrom(ref meshOutput, faces, ref spaGen, ref paiGen);
        }
    }
}