
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public struct CubeMeshGenerator<TIndexGen, TSpaceGen, TPaintGen>
        where TIndexGen : ICubeIndexGenerator<uint>
        where TSpaceGen : ICubeVertexGenerator<ChunkSpaceVertex>
        where TPaintGen : ICubeVertexGenerator<ChunkPaintVertex>
    {
        public static void GenerateFullFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TIndexGen indGen,
            ref TSpaceGen spaGen,
            ref TPaintGen paiGen)
        {
            meshOutput.Indices.PrepareCapacityFor(indGen.MaxIndices);
            meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices);
            meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices);

            indGen.AppendFirst(ref meshOutput.Indices, ref meshOutput.VertexOffset);
            spaGen.AppendFirst(ref meshOutput.SpaceVertices);
            paiGen.AppendFirst(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Top) != 0)
            {
                indGen.AppendTop(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendTop(ref meshOutput.SpaceVertices);
                paiGen.AppendTop(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Bottom) != 0)
            {
                indGen.AppendBottom(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendBottom(ref meshOutput.SpaceVertices);
                paiGen.AppendBottom(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Left) != 0)
            {
                indGen.AppendLeft(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendLeft(ref meshOutput.SpaceVertices);
                paiGen.AppendLeft(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Right) != 0)
            {
                indGen.AppendRight(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendRight(ref meshOutput.SpaceVertices);
                paiGen.AppendRight(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Front) != 0)
            {
                indGen.AppendFront(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendFront(ref meshOutput.SpaceVertices);
                paiGen.AppendFront(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Back) != 0)
            {
                indGen.AppendBack(ref meshOutput.Indices, ref meshOutput.VertexOffset);
                spaGen.AppendBack(ref meshOutput.SpaceVertices);
                paiGen.AppendBack(ref meshOutput.PaintVertices);
            }

            indGen.AppendLast(ref meshOutput.Indices, ref meshOutput.VertexOffset);
            spaGen.AppendLast(ref meshOutput.SpaceVertices);
            paiGen.AppendLast(ref meshOutput.PaintVertices);
        }

        public static void GenerateIndicesFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TIndexGen indGen)
        {
            meshOutput.Indices.PrepareCapacityFor(indGen.MaxIndices);

            indGen.AppendFirst(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Top) != 0)
                indGen.AppendTop(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Bottom) != 0)
                indGen.AppendBottom(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Left) != 0)
                indGen.AppendLeft(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Right) != 0)
                indGen.AppendRight(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Front) != 0)
                indGen.AppendFront(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Back) != 0)
                indGen.AppendBack(ref meshOutput.Indices, ref meshOutput.VertexOffset);

            indGen.AppendLast(ref meshOutput.Indices, ref meshOutput.VertexOffset);
        }

        public static void GenerateSpaceFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TSpaceGen spaGen)
        {
            meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices);

            spaGen.AppendFirst(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Top) != 0)
                spaGen.AppendTop(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Bottom) != 0)
                spaGen.AppendBottom(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Left) != 0)
                spaGen.AppendLeft(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Right) != 0)
                spaGen.AppendRight(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Front) != 0)
                spaGen.AppendFront(ref meshOutput.SpaceVertices);

            if ((faces & CubeFaces.Back) != 0)
                spaGen.AppendBack(ref meshOutput.SpaceVertices);

            spaGen.AppendLast(ref meshOutput.SpaceVertices);
        }

        public static void GeneratePaintFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TPaintGen paiGen)
        {
            meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices);

            paiGen.AppendFirst(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Top) != 0)
                paiGen.AppendTop(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Bottom) != 0)
                paiGen.AppendBottom(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Left) != 0)
                paiGen.AppendLeft(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Right) != 0)
                paiGen.AppendRight(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Front) != 0)
                paiGen.AppendFront(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Back) != 0)
                paiGen.AppendBack(ref meshOutput.PaintVertices);

            paiGen.AppendLast(ref meshOutput.PaintVertices);
        }

        public static void GenerateSpacePaintFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TSpaceGen spaGen,
            ref TPaintGen paiGen)
        {
            meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices);
            meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices);

            spaGen.AppendFirst(ref meshOutput.SpaceVertices);
            paiGen.AppendFirst(ref meshOutput.PaintVertices);

            if ((faces & CubeFaces.Top) != 0)
            {
                spaGen.AppendTop(ref meshOutput.SpaceVertices);
                paiGen.AppendTop(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Bottom) != 0)
            {
                spaGen.AppendBottom(ref meshOutput.SpaceVertices);
                paiGen.AppendBottom(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Left) != 0)
            {
                spaGen.AppendLeft(ref meshOutput.SpaceVertices);
                paiGen.AppendLeft(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Right) != 0)
            {
                spaGen.AppendRight(ref meshOutput.SpaceVertices);
                paiGen.AppendRight(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Front) != 0)
            {
                spaGen.AppendFront(ref meshOutput.SpaceVertices);
                paiGen.AppendFront(ref meshOutput.PaintVertices);
            }

            if ((faces & CubeFaces.Back) != 0)
            {
                spaGen.AppendBack(ref meshOutput.SpaceVertices);
                paiGen.AppendBack(ref meshOutput.PaintVertices);
            }

            spaGen.AppendLast(ref meshOutput.SpaceVertices);
            paiGen.AppendLast(ref meshOutput.PaintVertices);
        }
    }
}