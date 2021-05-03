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
            var indGen = new CubeIndexGenerator();
            var spaGen = new CubeSpaceVertexGenerator(position);
            var paiGen = new CubePaintVertexGenerator(anims[(uint)blockId], blockId * 2);

            meshState.Indices.PrepareCapacityFor(indGen.MaxIndicesPerBlock);
            meshState.SpaceVertices.PrepareCapacityFor(spaGen.MaxVerticesPerBlock);
            meshState.PaintVertices.PrepareCapacityFor(paiGen.MaxVerticesPerBlock);

            if ((faces & CubeFaces.Top) != 0)
            {
                indGen.AppendTop(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendTop(ref meshState.SpaceVertices);
                paiGen.AppendTop(ref meshState.PaintVertices);
            }

            if ((faces & CubeFaces.Bottom) != 0)
            {
                indGen.AppendBottom(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendBottom(ref meshState.SpaceVertices);
                paiGen.AppendBottom(ref meshState.PaintVertices);
            }

            if ((faces & CubeFaces.Left) != 0)
            {
                indGen.AppendLeft(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendLeft(ref meshState.SpaceVertices);
                paiGen.AppendLeft(ref meshState.PaintVertices);
            }

            if ((faces & CubeFaces.Right) != 0)
            {
                indGen.AppendRight(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendRight(ref meshState.SpaceVertices);
                paiGen.AppendRight(ref meshState.PaintVertices);
            }

            if ((faces & CubeFaces.Front) != 0)
            {
                indGen.AppendFront(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendFront(ref meshState.SpaceVertices);
                paiGen.AppendFront(ref meshState.PaintVertices);
            }

            if ((faces & CubeFaces.Back) != 0)
            {
                indGen.AppendBack(ref meshState.Indices, ref meshState.VertexOffset);
                spaGen.AppendBack(ref meshState.SpaceVertices);
                paiGen.AppendBack(ref meshState.PaintVertices);
            }
        }
    }
}