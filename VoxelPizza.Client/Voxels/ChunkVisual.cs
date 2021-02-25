using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public class ChunkVisual : GraphicsResource
    {
        private DeviceBuffer _worldAndInverseBuffer;
        private ResourceSet _chunkInfoSet;

        private DeviceBuffer _spaceBuffer;
        private DeviceBuffer _paintBuffer;
        private DeviceBuffer? _indexBuffer;

        public ChunkRenderer Renderer { get; }
        public Chunk Chunk { get; }

        public uint TriangleCount => _indexBuffer == null ? 0 : (_indexBuffer.SizeInBytes / 4) / 3;

        public ChunkVisual(ChunkRenderer chunkRenderer, Chunk chunk)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _worldAndInverseBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<WorldAndInverse>(), BufferUsage.UniformBuffer));

            _chunkInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                Renderer.ChunkInfoLayout,
                _worldAndInverseBuffer));

            ChunkMesher mesher = new();
            StoredChunkMesh mesh = mesher.Mesh(Chunk);

            if (mesh.Indices.Count != 0)
            {
                ReadOnlySpan<uint> indicesSpan = mesh.Indices.Span;
                _indexBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(indicesSpan).Length, BufferUsage.IndexBuffer));
                gd.UpdateBuffer(_indexBuffer, 0, indicesSpan);

                ReadOnlySpan<ChunkSpaceVertex> spaceVertexSpan = mesh.SpaceVertices.Span;
                _spaceBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(spaceVertexSpan).Length, BufferUsage.VertexBuffer));
                gd.UpdateBuffer(_spaceBuffer, 0, spaceVertexSpan);

                ReadOnlySpan<ChunkPaintVertex> paintVertexSpan = mesh.PaintVertices.Span;
                _paintBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(paintVertexSpan).Length, BufferUsage.VertexBuffer));
                gd.UpdateBuffer(_paintBuffer, 0, paintVertexSpan);

                WorldAndInverse worldAndInverse;
                worldAndInverse.World = Matrix4x4.CreateTranslation(Chunk.ChunkX * 16, Chunk.ChunkY * 16, Chunk.ChunkZ * 16);
                worldAndInverse.InverseWorld = VdUtilities.CalculateInverseTranspose(ref worldAndInverse.World);
                gd.UpdateBuffer(_worldAndInverseBuffer, 0, ref worldAndInverse);
            }

            mesh.Dispose();
        }

        public override void DestroyDeviceObjects()
        {
            _worldAndInverseBuffer?.Dispose();
            _chunkInfoSet?.Dispose();

            _indexBuffer?.Dispose();
            _indexBuffer = null;
            _spaceBuffer?.Dispose();
            _paintBuffer?.Dispose();
        }

        public void Render(GraphicsDevice gd, CommandList cl)
        {
            if (_indexBuffer == null)
                return;

            cl.SetGraphicsResourceSet(1, _chunkInfoSet);

            cl.SetVertexBuffer(0, _spaceBuffer);
            cl.SetVertexBuffer(1, _paintBuffer);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.DrawIndexed(_indexBuffer.SizeInBytes / 4, 1, 0, 0, 0);
        }
    }
}
