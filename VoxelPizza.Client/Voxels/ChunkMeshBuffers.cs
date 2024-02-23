using System.Collections.Generic;
using Veldrid;
using VoxelPizza.Memory;

namespace VoxelPizza.Client
{
    public class ChunkMeshBuffers
    {
        public DeviceBuffer _indirectBuffer;
        public DeviceBuffer _renderInfoBuffer;
        public DeviceBuffer _indexBuffer;
        public DeviceBuffer _vertexBuffer;

        public ArenaSegment IndirectSegment;
        public ArenaSegment RenderInfoSegment;

        public uint IndirectCount;
        public uint IndexCount;
        public uint VertexCount;
        public long SyncPoint;

        public List<DeviceBuffer> OldBuffers { get; } = new();

        public List<(ArenaSegment, ArenaAllocator)> OldIndexSegments { get; } = new();
        public List<(ArenaSegment, ArenaAllocator)> OldVertexSegments { get; } = new();

        public void Dispose()
        {
            IndirectCount = 0;
            IndexCount = 0;
            VertexCount = 0;

            _indirectBuffer = null!;
            _renderInfoBuffer = null!;
            _indexBuffer = null!;
            _vertexBuffer = null!;

            foreach (DeviceBuffer buffer in OldBuffers)
            {
                buffer.Dispose();
            }
            OldBuffers.Clear();
        }
    }
}
