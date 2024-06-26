using System;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe struct ChunkMeshResult
    {
        private void* _backingBuffer;
        private nuint _backingByteCapacity;

        private ByteStore<uint> _indices;
        private ByteStore<ChunkSpaceVertex> _spaceVertices;
        private ByteStore<ChunkPaintVertex> _paintVertices;

        public MemoryHeap? Heap { get; private set; }

        public readonly uint IndexCount => (uint)_indices.Count;
        public readonly uint VertexCount => (uint)_spaceVertices.Count;

        public readonly uint IndexByteCount => (uint)_indices.ByteCount;
        public readonly uint SpaceVertexByteCount => (uint)_spaceVertices.ByteCount;
        public readonly uint PaintVertexByteCount => (uint)_paintVertices.ByteCount;

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
            _backingByteCapacity = default;
            Heap = null;

            _indices = indices;
            _spaceVertices = spaceVertices;
            _paintVertices = paintVertices;
        }

        public static unsafe bool CreateCopyFrom(
            MemoryHeap heap,
            ByteStore<uint> indices,
            ByteStore<ChunkSpaceVertex> spaceVertices,
            ByteStore<ChunkPaintVertex> paintVertices,
            out ChunkMeshResult result)
        {
            if (heap == null)
                throw new ArgumentNullException(nameof(heap));

            nuint indexByteCount = indices.ByteCount;
            nuint spaceByteCount = spaceVertices.ByteCount;
            nuint paintByteCount = paintVertices.ByteCount;

            nuint byteCount = indexByteCount + spaceByteCount + paintByteCount;
            if (byteCount == 0)
            {
                result = default;
                return true;
            }

            nuint indexBlockSize = heap.GetBlockSize(indexByteCount);
            nuint spaceBlockSize = heap.GetBlockSize(spaceByteCount);
            nuint paintBlockSize = heap.GetBlockSize(paintByteCount);

            nuint totalBlockSize = indexBlockSize + spaceBlockSize + paintBlockSize;
            nuint unifiedTotalBlockSize = heap.GetBlockSize(byteCount);
            float sizeReductionFactor = totalBlockSize / (float)unifiedTotalBlockSize;
            if (sizeReductionFactor < 1)
            {
                if (indices.Clone(out ByteStore<uint> indicesClone) &&
                    spaceVertices.Clone(out ByteStore<ChunkSpaceVertex> spaceVerticesClone) &&
                    paintVertices.Clone(out ByteStore<ChunkPaintVertex> paintVerticesClone))
                {
                    result = new ChunkMeshResult(indicesClone, spaceVerticesClone, paintVerticesClone);
                    return true;
                }
                result = default;
                return false;
            }

            void* backingBuffer = heap.Alloc(byteCount, out nuint byteCapacity);
            byte* bytePtr = (byte*)backingBuffer;
            uint* indexPtr = (uint*)bytePtr;
            ChunkSpaceVertex* spacePtr = (ChunkSpaceVertex*)(bytePtr + indexByteCount);
            ChunkPaintVertex* paintPtr = (ChunkPaintVertex*)(bytePtr + indexByteCount + spaceByteCount);

            indices.Heap.Copy(indices.Buffer, indexPtr, indexByteCount);
            spaceVertices.Heap.Copy(spaceVertices.Buffer, spacePtr, spaceByteCount);
            paintVertices.Heap.Copy(paintVertices.Buffer, paintPtr, paintByteCount);

            ByteStore<uint> resultIndices = new(heap, indexPtr, indexByteCount);
            resultIndices.MoveByteHead(indexByteCount);

            ByteStore<ChunkSpaceVertex> resultSpaces = new(heap, spacePtr, spaceByteCount);
            resultSpaces.MoveByteHead(spaceByteCount);

            ByteStore<ChunkPaintVertex> resultPaints = new(heap, paintPtr, paintByteCount);
            resultPaints.MoveByteHead(paintByteCount);

            result = new ChunkMeshResult(resultIndices, resultSpaces, resultPaints)
            {
                Heap = heap,
                _backingBuffer = backingBuffer,
                _backingByteCapacity = byteCapacity,
            };
            return true;
        }

        public void Dispose()
        {
            if (Heap != null)
            {
                if (_backingBuffer != null)
                {
                    Heap.Free(_backingByteCapacity, _backingBuffer);
                    _backingBuffer = null;

                    _indices.Clear();
                    _spaceVertices.Clear();
                    _paintVertices.Clear();
                }
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
