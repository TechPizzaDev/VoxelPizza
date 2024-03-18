using System.Buffers;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public class BlockMemory
    {
        public uint[] Data;

        public Size3 InnerSize { get; }
        public Size3 OuterSize { get; }
        
        public ArrayBufferWriter<ChunkRegionPosition> RegionPosWriter { get; } = new();
        public ArrayBufferWriter<DimSlice> DimSliceWriter { get; } = new();
        public ArrayBufferWriter<ValueArc<ChunkRegion>> RegionArcWriter { get; } = new();
        
        public ArrayBufferWriter<ChunkInfo> ChunkInfoWriter { get; } = new();
        public ArrayBufferWriter<int> ChunkIndexWriter { get; } = new();
        public ArrayBufferWriter<ValueArc<Chunk>> ChunkArcWriter { get; } = new();

        public BlockMemory(Size3 innerSize, Size3 outerSize)
        {
            InnerSize = innerSize;
            OuterSize = outerSize;

            Data = new uint[OuterSize.Volume];
        }

        public static int GetIndexBase(int depth, int width, int y, int z)
        {
            return (y * depth + z) * width;
        }

        public static nuint GetIndexBase(nuint depth, nuint width, nuint y, nuint z)
        {
            return (y * depth + z) * width;
        }

        public struct DimSlice(BlockPosition origin, BlockPosition max)
        {
            public BlockPosition Origin = origin;
            public BlockPosition Max = max;
            public bool IsEmpty;
        }

        public struct ChunkInfo
        {
            public ChunkBoxSlice BoxSlice;
            public bool IsEmpty;
        }
    }
}
