using System;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    public ref struct MeshState
    {
        private Span<ByteStore<uint>> _indices;
        private Span<ByteStore<ChunkSpaceVertex>> _spaceVertices;
        private Span<ByteStore<ChunkPaintVertex>> _paintVertices;

        public uint VertexOffset;

        public ref ByteStore<uint> Indices => ref MemoryMarshal.GetReference(_indices);
        public ref ByteStore<ChunkSpaceVertex> SpaceVertices => ref MemoryMarshal.GetReference(_spaceVertices);
        public ref ByteStore<ChunkPaintVertex> PaintVertices => ref MemoryMarshal.GetReference(_paintVertices);

        public MeshState(
            ref ByteStore<uint> indices,
            ref ByteStore<ChunkSpaceVertex> spaceVertices,
            ref ByteStore<ChunkPaintVertex> paintVertices,
            uint vertexOffset)
        {
            _indices = MemoryMarshal.CreateSpan(ref indices, 1);
            _spaceVertices = MemoryMarshal.CreateSpan(ref spaceVertices, 1);
            _paintVertices = MemoryMarshal.CreateSpan(ref paintVertices, 1);
            VertexOffset = vertexOffset;
        }
    }
}