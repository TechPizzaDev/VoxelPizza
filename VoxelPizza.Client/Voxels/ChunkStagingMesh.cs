using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMesh : GraphicsResource
    {
        public uint ByteCount { get; }

        public DeviceBuffer Buffer { get; private set; }
        public ChunkMeshBase? Owner { get; set; }

        public ChunkStagingMesh(uint byteCount)
        {
            ByteCount = byteCount;
        }

        public static long totalbytesum = 0;

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            Buffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(ByteCount, BufferUsage.Staging));
        }

        public override void DestroyDeviceObjects()
        {
            Buffer?.Dispose();
            Buffer = null!;
        }
    }
}
