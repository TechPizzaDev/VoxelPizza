using System;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMesh : IDisposable
    {
        public uint MaxChunkCount { get; }

        public DeviceBuffer Buffer { get; private set; }

        public ChunkStagingMesh(ResourceFactory factory, uint maxChunkCount, uint byteCount)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            MaxChunkCount = maxChunkCount;

            Buffer = factory.CreateBuffer(new BufferDescription(byteCount, BufferUsage.Staging));
        }

        public static long totalbytesum = 0;

        public void Dispose()
        {
            Buffer.Dispose();
            Buffer = null!;
        }
    }
}
