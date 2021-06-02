using System;
using System.Runtime.CompilerServices;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ChunkStagingMesh : IDisposable
    {
        public int MaxChunkCount { get; }

        public int DrawCount { get; set; }
        public int IndexCount { get; set; }
        public int VertexCount { get; set; }

        public DeviceBuffer _indirectBuffer;
        public DeviceBuffer _translationBuffer;
        public DeviceBuffer _indexBuffer;
        public DeviceBuffer _spaceVertexBuffer;
        public DeviceBuffer _paintVertexBuffer;

        public ChunkStagingMesh(ResourceFactory factory, int maxChunkCount)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (maxChunkCount < 0)
                throw new ArgumentOutOfRangeException(nameof(maxChunkCount));
            MaxChunkCount = maxChunkCount;

            _indirectBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>() * (uint)MaxChunkCount, BufferUsage.Staging));

            _translationBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkInfo>() * (uint)MaxChunkCount, BufferUsage.VertexBuffer | BufferUsage.Dynamic));

            _indexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<uint>() * 98304 * 2, BufferUsage.IndexBuffer | BufferUsage.Dynamic));

            _spaceVertexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkSpaceVertex>() * 65535 * 2, BufferUsage.VertexBuffer | BufferUsage.Dynamic));

            _paintVertexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkPaintVertex>() * 65535 * 2, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        }

        public void Upload(
            ResourceFactory factory,
            CommandList commandList,
            ref DeviceBuffer indirectBuffer,
            ref DeviceBuffer translationBuffer,
            ref DeviceBuffer indexBuffer,
            ref DeviceBuffer spaceVertexBuffer,
            ref DeviceBuffer paintVertexBuffer)
        {
            if (IndexCount == 0)
                return;

            uint indirectSizeInBytes = (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>() * (uint)MaxChunkCount;
            if (indirectBuffer == null || indirectBuffer.SizeInBytes < indirectSizeInBytes)
            {
                indirectBuffer?.Dispose();
                indirectBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = indirectSizeInBytes,
                    Usage = BufferUsage.IndirectBuffer,
                });
            }

            uint translationSizeInBytes = (uint)Unsafe.SizeOf<ChunkInfo>() * (uint)MaxChunkCount;
            if (translationBuffer == null || translationBuffer.SizeInBytes < translationSizeInBytes)
            {
                translationBuffer?.Dispose();
                translationBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = translationSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }

            // TODO: smart checks for reallocations and
            //       separate commonly updated chunks into singular mesh instances

            uint indexSizeInBytes = (uint)IndexCount * sizeof(uint);
            //if (indexBuffer == null || indexBuffer.SizeInBytes < indexSizeInBytes)
            {
                indexBuffer?.Dispose();
                indexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = indexSizeInBytes,
                    Usage = BufferUsage.IndexBuffer,
                });
            }

            uint spaceVertexSizeInBytes = (uint)(VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            //if (spaceVertexBuffer == null || spaceVertexBuffer.SizeInBytes < spaceVertexSizeInBytes)
            {
                spaceVertexBuffer?.Dispose();
                spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = spaceVertexSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }

            uint paintVertexSizeInBytes = (uint)(VertexCount * Unsafe.SizeOf<ChunkPaintVertex>());
            //if (paintVertexBuffer == null || paintVertexBuffer.SizeInBytes < paintVertexSizeInBytes)
            {
                paintVertexBuffer?.Dispose();
                paintVertexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = paintVertexSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }

            commandList.CopyBuffer(_indirectBuffer, 0, indirectBuffer, 0, indirectSizeInBytes);
            commandList.CopyBuffer(_translationBuffer, 0, translationBuffer, 0, translationSizeInBytes);
            commandList.CopyBuffer(_indexBuffer, 0, indexBuffer, 0, indexSizeInBytes);
            commandList.CopyBuffer(_spaceVertexBuffer, 0, spaceVertexBuffer, 0, spaceVertexSizeInBytes);
            commandList.CopyBuffer(_paintVertexBuffer, 0, paintVertexBuffer, 0, paintVertexSizeInBytes);

            long bytesum = indirectSizeInBytes + translationSizeInBytes + indexSizeInBytes + spaceVertexSizeInBytes + paintVertexSizeInBytes;
            totalbytesum += bytesum;
        }

        public static long totalbytesum = 0;

        public void Map(
            GraphicsDevice graphicsDevice,
            out MappedResourceView<IndirectDrawIndexedArguments> indirectMap,
            out MappedResourceView<ChunkInfo> translationMap,
            out MappedResource indexMap,
            out MappedResourceView<ChunkSpaceVertex> spaceVertexMap,
            out MappedResourceView<ChunkPaintVertex> paintVertexMap)
        {
            indirectMap = graphicsDevice.Map<IndirectDrawIndexedArguments>(_indirectBuffer, MapMode.Write);
            translationMap = graphicsDevice.Map<ChunkInfo>(_translationBuffer, MapMode.Write);
            indexMap = graphicsDevice.Map(_indexBuffer, MapMode.Write);
            spaceVertexMap = graphicsDevice.Map<ChunkSpaceVertex>(_spaceVertexBuffer, MapMode.Write);
            paintVertexMap = graphicsDevice.Map<ChunkPaintVertex>(_paintVertexBuffer, MapMode.Write);
        }

        public void Unmap(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.Unmap(_indirectBuffer);
            graphicsDevice.Unmap(_translationBuffer);
            graphicsDevice.Unmap(_indexBuffer);
            graphicsDevice.Unmap(_spaceVertexBuffer);
            graphicsDevice.Unmap(_paintVertexBuffer);
        }

        public void Dispose()
        {
            _indirectBuffer.Dispose();
            _translationBuffer.Dispose();
            _indexBuffer.Dispose();
            _spaceVertexBuffer.Dispose();
            _paintVertexBuffer.Dispose();
        }
    }
}
