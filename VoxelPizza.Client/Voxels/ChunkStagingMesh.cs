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
                (uint)Unsafe.SizeOf<ChunkInfo>() * (uint)MaxChunkCount, BufferUsage.Staging));

            _indexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<uint>() * 98304, BufferUsage.Staging));

            _spaceVertexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkSpaceVertex>() * 65535, BufferUsage.Staging));

            _paintVertexBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkPaintVertex>() * 65535, BufferUsage.Staging));
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
            uint indirectSizeInBytes = (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>() * (uint)MaxChunkCount;
            if (indirectBuffer == null || indirectBuffer.SizeInBytes < indirectSizeInBytes)
            {
                indirectBuffer?.Dispose();

                if (indirectSizeInBytes != 0)
                {
                    indirectBuffer = factory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = indirectSizeInBytes,
                        Usage = BufferUsage.IndirectBuffer,
                    });
                }
            }

            uint translationSizeInBytes = (uint)Unsafe.SizeOf<ChunkInfo>() * (uint)MaxChunkCount;
            if (translationBuffer == null || translationBuffer.SizeInBytes < translationSizeInBytes)
            {
                translationBuffer?.Dispose();

                if (translationSizeInBytes != 0)
                {
                    translationBuffer = factory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = translationSizeInBytes,
                        Usage = BufferUsage.VertexBuffer,
                    });
                }
            }


            uint indexSizeInBytes = (uint)IndexCount * sizeof(uint);
            if (indexBuffer == null || indexBuffer.SizeInBytes < indexSizeInBytes)
            {
                indexBuffer?.Dispose();

                if (indexSizeInBytes != 0)
                {
                    indexBuffer = factory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = indexSizeInBytes,
                        Usage = BufferUsage.IndexBuffer,
                    });
                }
            }

            uint spaceVertexSizeInBytes = (uint)(VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            uint paintVertexSizeInBytes = (uint)(VertexCount * Unsafe.SizeOf<ChunkPaintVertex>());

            if (spaceVertexBuffer == null || spaceVertexBuffer.SizeInBytes < spaceVertexSizeInBytes)
            {
                spaceVertexBuffer?.Dispose();
                paintVertexBuffer?.Dispose();

                spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = spaceVertexSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });

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
        }

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
