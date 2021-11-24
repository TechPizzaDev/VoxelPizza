using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkMeshBuffers
    {
        public DeviceBuffer _indirectBuffer;
        public DeviceBuffer _renderInfoBuffer;
        public DeviceBuffer _indexBuffer;
        public DeviceBuffer _spaceVertexBuffer;
        public DeviceBuffer _paintVertexBuffer;

        public uint DrawCount;
        public uint IndexCount;
        public uint VertexCount;
        public long SyncPoint;

        public void Dispose()
        {
            DrawCount = 0;
            IndexCount = 0;
            VertexCount = 0;

            _indirectBuffer?.Dispose();
            _indirectBuffer = null!;

            _renderInfoBuffer?.Dispose();
            _renderInfoBuffer = null!;

            _indexBuffer?.Dispose();
            _indexBuffer = null!;

            _spaceVertexBuffer?.Dispose();
            _spaceVertexBuffer = null!;

            _paintVertexBuffer?.Dispose();
            _paintVertexBuffer = null!;
        }
    }
}
