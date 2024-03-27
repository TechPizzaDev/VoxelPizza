using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public class CubeMeshProvider : FaceDependentMeshProvider
    {
        // TODO: fix this temporary mess
        public TextureAnimation[] anims;

        /// <inheritdoc/>
        public override bool GenerateFull(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var indGen = new CubeIndexGenerator();
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(anims), (nint)blockId),
                blockId * 2);

            return CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateFullFrom(ref meshOutput, faces, ref indGen, ref spaGen, ref paiGen);
        }

        public override bool GenerateIndices(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var indGen = new CubeIndexGenerator();

            return CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateIndicesFrom(ref meshOutput, faces, ref indGen);
        }

        public override bool GenerateSpace(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            return CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateSpaceFrom(ref meshOutput, faces, ref spaGen);
        }

        public override bool GeneratePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(anims[blockId], blockId * 2);

            return CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GeneratePaintFrom(ref meshOutput, faces, ref paiGen);
        }

        public override bool GenerateSpacePaint(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState,
            CubeFaces faces)
        {
            var spaGen = new CubeSpaceVertexGenerator(mesherState.X, mesherState.Y, mesherState.Z);

            uint blockId = mesherState.CoreId;
            var paiGen = new CubePaintVertexGenerator(anims[blockId], blockId * 2);

            return CubeMeshGenerator<CubeIndexGenerator, CubeSpaceVertexGenerator, CubePaintVertexGenerator>
                .GenerateSpacePaintFrom(ref meshOutput, faces, ref spaGen, ref paiGen);
        }
    }
}