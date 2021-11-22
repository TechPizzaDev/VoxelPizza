using System;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public struct ChunkMeshResult
    {
        private IntPtr _backingBuffer;
        private uint _backingByteCount;

        private ByteStore<uint> _indices;
        private ByteStore<ChunkSpaceVertex> _spaceVertices;
        private ByteStore<ChunkPaintVertex> _paintVertices;

        public HeapPool? Pool { get; private set; }

        public readonly uint IndexCount => _indices.Count;
        public readonly uint VertexCount => _spaceVertices.Count;

        public readonly uint IndexByteCount => _indices.ByteCount;
        public readonly uint SpaceVertexByteCount => _spaceVertices.ByteCount;
        public readonly uint PaintVertexByteCount => _paintVertices.ByteCount;

        public readonly Span<uint> Indices => _indices.Span;
        public readonly Span<ChunkSpaceVertex> SpaceVertices => _spaceVertices.Span;
        public readonly Span<ChunkPaintVertex> PaintVertices => _paintVertices.Span;

        public readonly bool IsEmpty => IndexByteCount == 0;

        public ChunkMeshResult(
            ByteStore<uint> indices,
            ByteStore<ChunkSpaceVertex> spaceVertices,
            ByteStore<ChunkPaintVertex> paintVertices)
        {
            _backingBuffer = default;
            _backingByteCount = default;
            Pool = null;

            _indices = indices;
            _spaceVertices = spaceVertices;
            _paintVertices = paintVertices;
        }

        public static unsafe ChunkMeshResult CreateCopyFrom(
            HeapPool pool,
            ByteStore<uint> indices,
            ByteStore<ChunkSpaceVertex> spaceVertices,
            ByteStore<ChunkPaintVertex> paintVertices)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            uint indexByteCount = indices.ByteCount;
            uint spaceByteCount = spaceVertices.ByteCount;
            uint paintByteCount = paintVertices.ByteCount;

            uint minByteCapacity = indexByteCount + spaceByteCount + paintByteCount;
            if (minByteCapacity == 0)
                return default;

            uint indexPoolBlockSize = pool.GetBlockSize(indexByteCount);
            uint spacePoolBlockSize = pool.GetBlockSize(spaceByteCount);
            uint paintPoolBlockSize = pool.GetBlockSize(paintByteCount);
            HeapPool.Segment poolSegment = pool.GetSegment(minByteCapacity);

            uint splitBlocksTotalSize = indexPoolBlockSize + spacePoolBlockSize + paintPoolBlockSize;
            uint unifiedBlockTotalSize = poolSegment.BlockSize;
            float sizeReductionFactor = splitBlocksTotalSize / (float)unifiedBlockTotalSize;
            if (sizeReductionFactor < 1)
            {
                return new ChunkMeshResult(indices.Clone(), spaceVertices.Clone(), paintVertices.Clone());
            }

            IntPtr backingBuffer = poolSegment.Rent();
            var bytePtr = (byte*)backingBuffer;
            var indexPtr = (uint*)bytePtr;
            var spacePtr = (ChunkSpaceVertex*)(bytePtr + indexByteCount);
            var paintPtr = (ChunkPaintVertex*)(bytePtr + indexByteCount + spaceByteCount);

            Unsafe.CopyBlockUnaligned(indexPtr, indices.Buffer, indexByteCount);
            Unsafe.CopyBlockUnaligned(spacePtr, spaceVertices.Buffer, spaceByteCount);
            Unsafe.CopyBlockUnaligned(paintPtr, paintVertices.Buffer, paintByteCount);

            ByteStore<uint> resultIndices = new(pool, indexPtr, indexByteCount);
            resultIndices.MoveByteHead(indexByteCount);

            ByteStore<ChunkSpaceVertex> resultSpaces = new(pool, spacePtr, spaceByteCount);
            resultSpaces.MoveByteHead(spaceByteCount);

            ByteStore<ChunkPaintVertex> resultPaints = new(pool, paintPtr, paintByteCount);
            resultPaints.MoveByteHead(paintByteCount);

            return new ChunkMeshResult(resultIndices, resultSpaces, resultPaints)
            {
                Pool = pool,
                _backingBuffer = backingBuffer,
                _backingByteCount = unifiedBlockTotalSize,
            };
        }

        public void Dispose()
        {
            if (Pool != null)
            {
                Pool.Return(_backingByteCount, _backingBuffer);
                _backingBuffer = IntPtr.Zero;

                _indices.Clear();
                _spaceVertices.Clear();
                _paintVertices.Clear();
            }
            else
            {
                _indices.Dispose();
                _spaceVertices.Dispose();
                _paintVertices.Dispose();
            }
        }
    }
}
