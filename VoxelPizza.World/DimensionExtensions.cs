using System;
using VoxelPizza.Collections;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public static class DimensionExtensions
    {
        public static BlockMemoryState FetchBlockMemory(
            this Dimension dimension, BlockMemory blockBuffer, BlockPosition origin)
        {
            Span<uint> data = blockBuffer.Data.AsSpan();
            Size3 outerSize = blockBuffer.OuterSize;
            Size3 innerSize = blockBuffer.InnerSize;
            uint xOffset = (outerSize.W - innerSize.W) / 2;
            uint yOffset = (outerSize.H - innerSize.H) / 2;
            uint zOffset = (outerSize.D - innerSize.D) / 2;

            BlockPosition blockOffset = new(
                origin.X - (int)xOffset,
                origin.Y - (int)yOffset,
                origin.Z - (int)zOffset);
            WorldBox fetchBox = new(blockOffset, outerSize);

            ChunkBoxSliceEnumerator chunkBoxEnumerator = fetchBox.EnumerateChunkBoxSlices();
            int maxChunkCount = chunkBoxEnumerator.GetMaxChunkCount();

            Span<ChunkBoxSlice> chunkBoxes = blockBuffer.GetChunkBoxBuffer(maxChunkCount);
            Span<bool> emptyChunks = blockBuffer.GetEmptyChunkBuffer(maxChunkCount);

            int chunkCount = 0;
            int emptyCount = 0;

            foreach (ChunkBoxSlice chunkBox in chunkBoxEnumerator)
            {
                chunkBoxes[chunkCount++] = chunkBox;
            }

            chunkBoxes = chunkBoxes[..chunkCount];
            emptyChunks = emptyChunks[..chunkCount];

            for (int i = 0; i < chunkCount; i++)
            {
                ref ChunkBoxSlice chunkBox = ref chunkBoxes[i];
                using RefCounted<Chunk?> countedChunk = dimension.GetChunk(chunkBox.Chunk);

                if (!countedChunk.TryGetValue(out Chunk? chunk) || chunk.IsEmpty)
                {
                    emptyCount++;
                    emptyChunks[i] = true;
                    continue;
                }
                emptyChunks[i] = false;

                int outerOriginX = chunkBox.OuterOrigin.X;
                int outerOriginY = chunkBox.OuterOrigin.Y;
                int outerOriginZ = chunkBox.OuterOrigin.Z;
                int outerSizeD = (int)outerSize.D;
                int outerSizeW = (int)outerSize.W;
                int innerSizeH = (int)chunkBox.Size.H;
                int innerSizeD = (int)chunkBox.Size.D;
                int innerSizeW = (int)chunkBox.Size.W;

                int innerOriginX = chunkBox.InnerOrigin.X;
                int innerOriginY = chunkBox.InnerOrigin.Y;
                int innerOriginZ = chunkBox.InnerOrigin.Z;

                BlockStorage storage = chunk.GetBlockStorage();

                for (int y = 0; y < innerSizeH; y++)
                {
                    for (int z = 0; z < innerSizeD; z++)
                    {
                        int outerBaseIndex = BlockMemory.GetIndexBase(
                            outerSizeD,
                            outerSizeW,
                            y + outerOriginY,
                            z + outerOriginZ)
                            + outerOriginX;

                        Span<uint> destination = data.Slice(outerBaseIndex, innerSizeW);

                        storage.GetBlockRow(
                            innerOriginX,
                            y + innerOriginY,
                            z + innerOriginZ,
                            destination);
                    }
                }
            }

            if (emptyCount == chunkCount)
            {
                return BlockMemoryState.Uninitialized;
            }

            if (emptyCount == 0)
            {
                return BlockMemoryState.Filled;
            }

            for (int i = 0; i < chunkCount; i++)
            {
                if (!emptyChunks[i])
                {
                    continue;
                }

                ref ChunkBoxSlice chunkBox = ref chunkBoxes[i];
                int outerOriginX = chunkBox.OuterOrigin.X;
                int outerOriginY = chunkBox.OuterOrigin.Y;
                int outerOriginZ = chunkBox.OuterOrigin.Z;
                int outerSizeD = (int)outerSize.D;
                int outerSizeW = (int)outerSize.W;
                int innerSizeH = (int)chunkBox.Size.H;
                int innerSizeD = (int)chunkBox.Size.D;
                int innerSizeW = (int)chunkBox.Size.W;

                for (int y = 0; y < innerSizeH; y++)
                {
                    for (int z = 0; z < innerSizeD; z++)
                    {
                        int outerBaseIndex = BlockMemory.GetIndexBase(
                            outerSizeD,
                            outerSizeW,
                            y + outerOriginY,
                            z + outerOriginZ)
                            + outerOriginX;

                        data.Slice(outerBaseIndex, innerSizeW).Clear();
                    }
                }
            }
            return BlockMemoryState.Filled;
        }

    }
}
