using System;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class BlockMemory
    {
        public uint[] Data;

        private ChunkBoxSlice[] _chunkBoxBuffer;
        private bool[] _emptyChunkBuffer;

        public Size3 InnerSize { get; }
        public Size3 OuterSize { get; }

        public BlockMemory(Size3 innerSize, Size3 outerSize)
        {
            InnerSize = innerSize;
            OuterSize = outerSize;

            Data = new uint[OuterSize.Volume];

            _chunkBoxBuffer = Array.Empty<ChunkBoxSlice>();
            _emptyChunkBuffer = Array.Empty<bool>();
        }

        public Span<ChunkBoxSlice> GetChunkBoxBuffer(int size)
        {
            if (_chunkBoxBuffer.Length < size)
            {
                _chunkBoxBuffer = GC.AllocateUninitializedArray<ChunkBoxSlice>(size);
            }
            Span<ChunkBoxSlice> span = _chunkBoxBuffer.AsSpan(0, size);
            return span;
        }

        public Span<bool> GetEmptyChunkBuffer(int size)
        {
            if (_emptyChunkBuffer.Length < size)
            {
                _emptyChunkBuffer = GC.AllocateUninitializedArray<bool>(size);
            }
            Span<bool> span = _emptyChunkBuffer.AsSpan(0, size);
            return span;
        }

        public static nuint GetIndexBase(nuint depth, nuint width, nuint y, nuint z)
        {
            return (y * depth + z) * width;
        }
    }
}
