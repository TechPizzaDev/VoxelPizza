using System;
using System.Buffers;
using System.Diagnostics;
using VoxelPizza.Collections;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public static class DimensionExtensions
    {
        private static bool GetRegions(
            ValueArc<Dimension> dimension,
            BlockMemory blockBuffer,
            BlockPosition dimOrigin,
            out Span<BlockMemory.DimSlice> dimSlices,
            out Span<ValueArc<ChunkRegion>> regionArcs)
        {
            Dimension dim = dimension.Get();

            DimensionBox dimBox = new(dimOrigin, blockBuffer.OuterSize);

            ArrayBufferWriter<ChunkRegionPosition> regionPosWriter = blockBuffer.RegionPosWriter;
            ArrayBufferWriter<BlockMemory.DimSlice> dimSliceWriter = blockBuffer.DimSliceWriter;
            ArrayBufferWriter<ValueArc<ChunkRegion>> regionArcWriter = blockBuffer.RegionArcWriter;

            regionPosWriter.Clear();
            dimSliceWriter.Clear();
            regionArcWriter.Clear();

            DimensionBox.Enumerator dimSliceEnumerator = new(dimBox.Origin, dimBox.Max);
            int maxRegionCount = dimSliceEnumerator.GetMaxRegionCount();

            int regionCount = 0;
            Span<ChunkRegionPosition> regionPosSpan = regionPosWriter.GetSpan(maxRegionCount);
            Span<BlockMemory.DimSlice> dimSliceSpan = dimSliceWriter.GetSpan(maxRegionCount);

            foreach (DimensionBoxSlice dimSlice in dimSliceEnumerator)
            {
                regionPosSpan[regionCount] = dimSlice.Region;

                (BlockPosition sliceOrigin, BlockPosition sliceMax) = dimSlice.GetOriginAndMax();
                dimSliceSpan[regionCount] = new BlockMemory.DimSlice(sliceOrigin, sliceMax);

                regionCount++;
            }

            regionPosWriter.Advance(regionCount);
            dimSliceWriter.Advance(regionCount);

            dimSlices = dimSliceSpan.Slice(0, regionCount);
            regionArcs = regionArcWriter.GetSpan(regionCount).Slice(0, regionCount);

            int getRegionCount = dim.GetRegions(regionPosSpan.Slice(0, regionCount), regionArcs);
            return getRegionCount != 0;
        }

        public static BlockMemoryState FetchBlockMemory(
            this ValueArc<Dimension> dimension, BlockMemory blockBuffer, BlockPosition origin)
        {
            Size3 outerSize = blockBuffer.OuterSize;
            Size3 innerSize = blockBuffer.InnerSize;
            Size3 offsetSize = outerSize - innerSize;

            BlockPosition dimOrigin = new(
                origin.X - (int)(offsetSize.W / 2),
                origin.Y - (int)(offsetSize.H / 2),
                origin.Z - (int)(offsetSize.D / 2));

            if (!GetRegions(
                dimension,
                blockBuffer,
                dimOrigin,
                out Span<BlockMemory.DimSlice> dimSlices,
                out Span<ValueArc<ChunkRegion>> regionArcs))
            {
                return BlockMemoryState.Uninitialized;
            }

            ArrayBufferWriter<BlockMemory.ChunkInfo> chunkInfoWriter = blockBuffer.ChunkInfoWriter;
            ArrayBufferWriter<int> chunkIndexWriter = blockBuffer.ChunkIndexWriter;
            ArrayBufferWriter<ValueArc<Chunk>> chunkArcWriter = blockBuffer.ChunkArcWriter;

            chunkInfoWriter.Clear();
            chunkIndexWriter.Clear();
            chunkArcWriter.Clear();

            int regionCount = dimSlices.Length;
            int chunkCount = 0;
            int emptyRegionCount = 0;
            int emptyChunkCount = 0;

            for (int regIdx = 0; regIdx < regionCount; regIdx++)
            {
                ref BlockMemory.DimSlice dimSlice = ref dimSlices[regIdx];
                ref ValueArc<ChunkRegion> regionArc = ref regionArcs[regIdx];
                if (!regionArc.TryGet(out ChunkRegion? region))
                {
                    emptyRegionCount++;
                    dimSlice.IsEmpty = true;
                    continue;
                }

                (int count, int emptyCount) = GetChunks(region, dimSlice, blockBuffer, dimOrigin);
                if (emptyCount == count)
                {
                    emptyRegionCount++;
                    dimSlice.IsEmpty = true;
                }
                else
                {
                    dimSlice.IsEmpty = false;
                }

                chunkCount += count;
                emptyChunkCount += emptyCount;
            }

            if (emptyChunkCount == chunkCount)
            {
                Debug.Assert(chunkInfoWriter.WrittenCount == 0);
                Debug.Assert(emptyRegionCount == regionCount);
                return BlockMemoryState.Uninitialized;
            }

            if (emptyChunkCount == 0)
            {
                return BlockMemoryState.Filled;
            }

            return ClearSlices(blockBuffer, dimOrigin);
        }

        private static (int Count, int EmptyCount) GetChunks(
            ChunkRegion chunkRegion,
            in BlockMemory.DimSlice dimSlice,
            BlockMemory blockBuffer,
            BlockPosition dimOrigin)
        {
            ArrayBufferWriter<BlockMemory.ChunkInfo> chunkInfoWriter = blockBuffer.ChunkInfoWriter;
            ArrayBufferWriter<int> chunkIndexWriter = blockBuffer.ChunkIndexWriter;
            ArrayBufferWriter<ValueArc<Chunk>> chunkArcWriter = blockBuffer.ChunkArcWriter;

            chunkIndexWriter.Clear();
            chunkArcWriter.Clear();

            ChunkBoxSliceEnumerator chunkBoxEnumerator = new(dimSlice.Origin, dimSlice.Max);
            int maxChunkCount = chunkBoxEnumerator.GetMaxChunkCount();
            Span<BlockMemory.ChunkInfo> chunkInfoSpan = chunkInfoWriter.GetSpan(maxChunkCount);
            Span<int> chunkIndexSpan = chunkIndexWriter.GetSpan(maxChunkCount);

            int chunkCount = 0;
            foreach (ChunkBoxSlice chunkBox in chunkBoxEnumerator)
            {
                chunkInfoSpan[chunkCount].BoxSlice = chunkBox;

                ChunkPosition localPosition = ChunkRegion.GetLocalChunkPosition(chunkBox.Chunk);
                int localChunkIndex = ChunkRegion.GetChunkIndex(localPosition);
                chunkIndexSpan[chunkCount] = localChunkIndex;

                chunkCount++;
            }

            chunkInfoSpan = chunkInfoSpan.Slice(0, chunkCount);
            chunkIndexSpan = chunkIndexSpan.Slice(0, chunkCount);

            Span<ValueArc<Chunk>> chunkArcs = chunkArcWriter.GetSpan(chunkCount).Slice(0, chunkCount);
            int getChunkCount = chunkRegion.GetLocalChunks(chunkIndexSpan, chunkArcs, true);
            if (getChunkCount == 0)
            {
                return (chunkCount, chunkCount);
            }

            // Only advance the writer when producing meaningful ChunkInfo
            chunkInfoWriter.Advance(chunkCount);

            Span<uint> bufferData = blockBuffer.Data.AsSpan();
            Size3 outerSize = blockBuffer.OuterSize;
            int emptyChunkCount = 0;

            for (int chunkIdx = 0; chunkIdx < chunkCount; chunkIdx++)
            {
                ref BlockMemory.ChunkInfo chunkInfo = ref chunkInfoSpan[chunkIdx];

                using ValueArc<Chunk> chunkArc = chunkArcs[chunkIdx];
                if (!chunkArc.TryGet(out Chunk? chunk) || chunk.IsEmpty)
                {
                    emptyChunkCount++;
                    chunkInfo.IsEmpty = true;
                    continue;
                }
                chunkInfo.IsEmpty = false;

                ref readonly ChunkBoxSlice chunkBox = ref chunkInfo.BoxSlice;
                BlockPosition outerOrigin = chunkBox.Block - dimOrigin;
                uint boxSizeH = chunkBox.Size.H;
                uint boxSizeD = chunkBox.Size.D;
                uint boxSizeW = chunkBox.Size.W;

                int innerOriginX = chunkBox.InnerOrigin.X;
                int innerOriginY = chunkBox.InnerOrigin.Y;
                int innerOriginZ = chunkBox.InnerOrigin.Z;

                BlockStorage storage = chunk.GetBlockStorage();

                for (uint y = 0; y < boxSizeH; y++)
                {
                    for (uint z = 0; z < boxSizeD; z++)
                    {
                        int outerBaseIndex = (int)BlockMemory.GetIndexBase(
                            outerSize.D,
                            outerSize.W,
                            y + (uint)outerOrigin.Y,
                            z + (uint)outerOrigin.Z)
                            + outerOrigin.X;

                        Span<uint> destination = bufferData.Slice(outerBaseIndex, (int)boxSizeW);

                        storage.GetBlockRow(
                            innerOriginX,
                            (int)y + innerOriginY,
                            (int)z + innerOriginZ,
                            destination);
                    }
                }
            }

            return (chunkCount, emptyChunkCount);
        }

        private static BlockMemoryState ClearSlices(BlockMemory blockBuffer, BlockPosition dimOrigin)
        {
            Size3 outerSize = blockBuffer.OuterSize;
            Span<uint> bufferData = blockBuffer.Data.AsSpan();
            ReadOnlySpan<BlockMemory.DimSlice> dimSliceSpan = blockBuffer.DimSliceWriter.WrittenSpan;
            ReadOnlySpan<BlockMemory.ChunkInfo> chunkInfoSpan = blockBuffer.ChunkInfoWriter.WrittenSpan;

            foreach (ref readonly BlockMemory.DimSlice dimSlice in dimSliceSpan)
            {
                if (!dimSlice.IsEmpty)
                {
                    continue;
                }

                Size3 dimInnerSize = (dimSlice.Max - dimSlice.Origin).ToSize3();
                if (dimInnerSize == outerSize)
                {
                    // The fetched region(slice) is contained entirely within a single empty region.
                    Debug.Assert(dimSliceSpan.Length == 1);
                    return BlockMemoryState.Uninitialized;
                }

                BlockPosition outerOrigin = dimSlice.Origin - dimOrigin;
                ClearBox(outerOrigin, outerSize, dimInnerSize, bufferData);
            }

            foreach (ref readonly BlockMemory.ChunkInfo chunkInfo in chunkInfoSpan)
            {
                if (!chunkInfo.IsEmpty)
                {
                    continue;
                }

                ref readonly ChunkBoxSlice chunkBox = ref chunkInfo.BoxSlice;
                if (chunkBox.Size == outerSize)
                {
                    // Rare case where the buffer only fits at most a chunk(slice) *and* that chunk was empty.
                    Debug.Assert(chunkInfoSpan.Length == 1);
                    return BlockMemoryState.Uninitialized;
                }

                BlockPosition outerOrigin = chunkBox.Block - dimOrigin;
                ClearBox(outerOrigin, outerSize, chunkBox.Size, bufferData);
            }

            return BlockMemoryState.Filled;
        }

        private static void ClearBox(BlockPosition outerOrigin, Size3 outerSize, Size3 innerSize, Span<uint> bufferData)
        {
            int outerSizeW = (int)outerSize.W;
            int outerSizeD = (int)outerSize.D;
            int innerSizeH = (int)innerSize.H;
            int innerSizeD = (int)innerSize.D;
            int innerSizeW = (int)innerSize.W;

            for (int y = 0; y < innerSizeH; y++)
            {
                for (int z = 0; z < innerSizeD; z++)
                {
                    int outerBaseIndex = BlockMemory.GetIndexBase(
                        outerSizeD,
                        outerSizeW,
                        y + outerOrigin.Y,
                        z + outerOrigin.Z)
                        + outerOrigin.X;

                    bufferData.Slice(outerBaseIndex, innerSizeW).Clear();
                }
            }
        }
    }
}
