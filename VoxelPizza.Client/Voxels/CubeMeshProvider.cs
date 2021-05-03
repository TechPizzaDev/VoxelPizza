using System.Numerics;

namespace VoxelPizza.Client
{
    public class CubeMeshProvider : CullableMeshProvider
    {
        // TODO: fix this temporary mess
        public TextureAnimation[] anims;

        public override void Provide(
            ref MeshState meshState,
            uint blockId,
            Vector3 position,
            CubeFaces faces)
        {
            var spaGen = new CubeSpaceVertexGenerator(position);
            var paiGen = new CubePaintVertexGenerator(anims[(uint)blockId], blockId * 2);

            var spaPro = new CubeVertexProvider<CubeSpaceVertexGenerator, ChunkSpaceVertex>(spaGen, faces);
            var paiPro = new CubeVertexProvider<CubePaintVertexGenerator, ChunkPaintVertex>(paiGen, faces);

            var indGen = new CubeIndexGenerator();
            var indPro = new CubeIndexProvider<CubeIndexGenerator, uint>(indGen, faces);

            spaPro.AppendVertices(ref meshState.SpaceVertices);
            paiPro.AppendVertices(ref meshState.PaintVertices);
            indPro.AppendIndices(ref meshState.Indices, ref meshState.VertexOffset);
        }
    }
}