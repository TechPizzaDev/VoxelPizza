
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe struct CubeMeshGenerator<TIndexGen, TSpaceGen, TPaintGen>
        where TIndexGen : ICubeIndexGenerator<uint>
        where TSpaceGen : ICubeVertexGenerator<ChunkSpaceVertex>
        where TPaintGen : ICubeVertexGenerator<ChunkPaintVertex>
    {
        /// <returns>
        /// <see langword="true"/> when memory allocation succeeded; 
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool GenerateFullFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TIndexGen indGen,
            ref TSpaceGen spaGen,
            ref TPaintGen paiGen)
        {
            if (!meshOutput.Indices.PrepareCapacityFor(indGen.MaxIndices) ||
                !meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices) ||
                !meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices))
            {
                return false;
            }

            uint* indices = meshOutput.Indices.Head;
            ChunkSpaceVertex* spaceVertices = meshOutput.SpaceVertices.Head;
            ChunkPaintVertex* paintVertices = meshOutput.PaintVertices.Head;

            indices += indGen.AppendFirst(indices, ref meshOutput.VertexOffset);
            spaceVertices += spaGen.AppendFirst(spaceVertices);
            paintVertices += paiGen.AppendFirst(paintVertices);

            if ((faces & CubeFaces.Top) != 0)
            {
                indices += indGen.AppendTop(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendTop(spaceVertices);
                paintVertices += paiGen.AppendTop(paintVertices);
            }

            if ((faces & CubeFaces.Bottom) != 0)
            {
                indices += indGen.AppendBottom(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendBottom(spaceVertices);
                paintVertices += paiGen.AppendBottom(paintVertices);
            }

            if ((faces & CubeFaces.Left) != 0)
            {
                indices += indGen.AppendLeft(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendLeft(spaceVertices);
                paintVertices += paiGen.AppendLeft(paintVertices);
            }

            if ((faces & CubeFaces.Right) != 0)
            {
                indices += indGen.AppendRight(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendRight(spaceVertices);
                paintVertices += paiGen.AppendRight(paintVertices);
            }

            if ((faces & CubeFaces.Front) != 0)
            {
                indices += indGen.AppendFront(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendFront(spaceVertices);
                paintVertices += paiGen.AppendFront(paintVertices);
            }

            if ((faces & CubeFaces.Back) != 0)
            {
                indices += indGen.AppendBack(indices, ref meshOutput.VertexOffset);
                spaceVertices += spaGen.AppendBack(spaceVertices);
                paintVertices += paiGen.AppendBack(paintVertices);
            }

            indices += indGen.AppendLast(indices, ref meshOutput.VertexOffset);
            spaceVertices += spaGen.AppendLast(spaceVertices);
            paintVertices += paiGen.AppendLast(paintVertices);

            meshOutput.Indices.Head = indices;
            meshOutput.SpaceVertices.Head = spaceVertices;
            meshOutput.PaintVertices.Head = paintVertices;

            return true;
        }

        public static bool GenerateIndicesFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TIndexGen indGen)
        {
            if (!meshOutput.Indices.PrepareCapacityFor(indGen.MaxIndices))
            {
                return false;
            }

            uint* indices = meshOutput.Indices.Head;

            indices += indGen.AppendFirst(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Top) != 0)
                indices += indGen.AppendTop(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Bottom) != 0)
                indices += indGen.AppendBottom(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Left) != 0)
                indices += indGen.AppendLeft(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Right) != 0)
                indices += indGen.AppendRight(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Front) != 0)
                indices += indGen.AppendFront(indices, ref meshOutput.VertexOffset);

            if ((faces & CubeFaces.Back) != 0)
                indices += indGen.AppendBack(indices, ref meshOutput.VertexOffset);

            indices += indGen.AppendLast(indices, ref meshOutput.VertexOffset);

            meshOutput.Indices.Head = indices;

            return true;
        }

        public static bool GenerateSpaceFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TSpaceGen spaGen)
        {
            if (!meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices))
            {
                return false;
            }

            ChunkSpaceVertex* spaceVertices = meshOutput.SpaceVertices.Head;

            spaceVertices += spaGen.AppendFirst(spaceVertices);

            if ((faces & CubeFaces.Top) != 0)
                spaceVertices += spaGen.AppendTop(spaceVertices);

            if ((faces & CubeFaces.Bottom) != 0)
                spaceVertices += spaGen.AppendBottom(spaceVertices);

            if ((faces & CubeFaces.Left) != 0)
                spaceVertices += spaGen.AppendLeft(spaceVertices);

            if ((faces & CubeFaces.Right) != 0)
                spaceVertices += spaGen.AppendRight(spaceVertices);

            if ((faces & CubeFaces.Front) != 0)
                spaceVertices += spaGen.AppendFront(spaceVertices);

            if ((faces & CubeFaces.Back) != 0)
                spaceVertices += spaGen.AppendBack(spaceVertices);

            spaceVertices += spaGen.AppendLast(spaceVertices);

            meshOutput.SpaceVertices.Head = spaceVertices;

            return true;
        }

        public static bool GeneratePaintFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TPaintGen paiGen)
        {
            if (!meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices))
            {
                return false;
            }

            ChunkPaintVertex* paintVertices = meshOutput.PaintVertices.Head;

            paintVertices += paiGen.AppendFirst(paintVertices);

            if ((faces & CubeFaces.Top) != 0)
                paintVertices += paiGen.AppendTop(paintVertices);

            if ((faces & CubeFaces.Bottom) != 0)
                paintVertices += paiGen.AppendBottom(paintVertices);

            if ((faces & CubeFaces.Left) != 0)
                paintVertices += paiGen.AppendLeft(paintVertices);

            if ((faces & CubeFaces.Right) != 0)
                paintVertices += paiGen.AppendRight(paintVertices);

            if ((faces & CubeFaces.Front) != 0)
                paintVertices += paiGen.AppendFront(paintVertices);

            if ((faces & CubeFaces.Back) != 0)
                paintVertices += paiGen.AppendBack(paintVertices);

            paintVertices += paiGen.AppendLast(paintVertices);

            meshOutput.PaintVertices.Head = paintVertices;

            return false;
        }

        public static bool GenerateSpacePaintFrom(
            ref ChunkMeshOutput meshOutput,
            CubeFaces faces,
            ref TSpaceGen spaGen,
            ref TPaintGen paiGen)
        {
            if (!meshOutput.SpaceVertices.PrepareCapacityFor(spaGen.MaxVertices) ||
                !meshOutput.PaintVertices.PrepareCapacityFor(paiGen.MaxVertices))
            {
                return false;
            }

            ChunkSpaceVertex* spaceVertices = meshOutput.SpaceVertices.Head;
            ChunkPaintVertex* paintVertices = meshOutput.PaintVertices.Head;

            spaceVertices += spaGen.AppendFirst(spaceVertices);
            paintVertices += paiGen.AppendFirst(paintVertices);

            if ((faces & CubeFaces.Top) != 0)
            {
                spaceVertices += spaGen.AppendTop(spaceVertices);
                paintVertices += paiGen.AppendTop(paintVertices);
            }

            if ((faces & CubeFaces.Bottom) != 0)
            {
                spaceVertices += spaGen.AppendBottom(spaceVertices);
                paintVertices += paiGen.AppendBottom(paintVertices);
            }

            if ((faces & CubeFaces.Left) != 0)
            {
                spaceVertices += spaGen.AppendLeft(spaceVertices);
                paintVertices += paiGen.AppendLeft(paintVertices);
            }

            if ((faces & CubeFaces.Right) != 0)
            {
                spaceVertices += spaGen.AppendRight(spaceVertices);
                paintVertices += paiGen.AppendRight(paintVertices);
            }

            if ((faces & CubeFaces.Front) != 0)
            {
                spaceVertices += spaGen.AppendFront(spaceVertices);
                paintVertices += paiGen.AppendFront(paintVertices);
            }

            if ((faces & CubeFaces.Back) != 0)
            {
                spaceVertices += spaGen.AppendBack(spaceVertices);
                paintVertices += paiGen.AppendBack(paintVertices);
            }

            spaceVertices += spaGen.AppendLast(spaceVertices);
            paintVertices += paiGen.AppendLast(paintVertices);

            meshOutput.SpaceVertices.Head = spaceVertices;
            meshOutput.PaintVertices.Head = paintVertices;

            return true;
        }
    }
}