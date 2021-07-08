using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkMesh : ChunkMeshBase
    {
        private ResourceSet _chunkInfoSet;

        private ChunkMeshResult _mesh;
        private int _buildRequired;
        private bool _uploadRequired;
        private int _indexCount;
        private int _vertexCount;

        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _renderInfoBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;

        public ChunkRenderer Renderer { get; }
        public ChunkPosition Position { get; }

        public override int IndexCount => _indexCount;
        public override int VertexCount => _vertexCount;

        public override bool IsBuildRequired => _buildRequired > 0;
        public override bool IsUploadRequired => _uploadRequired;

        public ChunkMesh(ChunkRenderer chunkRenderer, ChunkPosition position)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _renderInfoBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<Vector4>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            var chunkInfo = new ChunkRenderInfo(new Vector4(
                Position.X * Chunk.Width,
                Position.Y * Chunk.Height,
                Position.Z * Chunk.Depth,
                0));

            cl.UpdateBuffer(_renderInfoBuffer, 0, ref chunkInfo);

            _chunkInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                Renderer.ChunkInfoLayout,
                _renderInfoBuffer));

            RequestBuild();
        }

        public override void DestroyDeviceObjects()
        {
            DisposeMeshBuffers();

            _renderInfoBuffer?.Dispose();
            _renderInfoBuffer = null!;

            _chunkInfoSet?.Dispose();
            _chunkInfoSet = null!;
        }

        public override (int Total, int ToBuild, int ToUpload) GetMeshCount()
        {
            return (1, _buildRequired != 0 ? 1 : 0, _uploadRequired ? 1 : 0);
        }

        public override void RequestBuild(ChunkPosition position)
        {
            Debug.Assert(position == Position);

            RequestBuild();
        }

        private void RequestBuild()
        {
            Interlocked.Increment(ref _buildRequired);
        }

        public override bool Build(ChunkMesher mesher, BlockMemory blockMemoryBuffer)
        {
            int buildRequired = _buildRequired;
            if (buildRequired <= 0)
            {
                return false;
            }

            _mesh.Dispose();

            Renderer.FetchBlockMemory(blockMemoryBuffer, Position.ToBlock());

            _mesh = Renderer.ChunkMesher.Mesh(blockMemoryBuffer);

            _uploadRequired = true;

            Interlocked.Add(ref _buildRequired, -buildRequired);
            return true;
        }

        public override bool Upload(
            GraphicsDevice gd, CommandList uploadList, ChunkStagingMeshPool stagingMeshPool,
            out ChunkStagingMesh? stagingMesh)
        {
            if (!_uploadRequired)
            {
                stagingMesh = null;
                return true;
            }

            int indexCountRequired = 0;
            int spaceVertexBytesRequired = 0;
            int paintVertexBytesRequired = 0;

            ref ChunkMeshResult mesh = ref _mesh;

            int indexCount = mesh.IndexCount;
            if (indexCount > 0)
            {
                indexCountRequired += indexCount;
                spaceVertexBytesRequired += mesh.SpaceVertexByteCount;
                paintVertexBytesRequired += mesh.PaintVertexByteCount;
            }

            if (indexCountRequired <= 0)
            {
                DisposeMeshBuffers();

                _uploadRequired = false;
                stagingMesh = null;
                return true;
            }

            // TODO: rent based on required bytes

            if (!stagingMeshPool.TryRent(
                out stagingMesh,
                1))
            {
                return false;
            }

            try
            {
                stagingMesh.Map(
                    gd,
                    out MappedResource indexMap,
                    out MappedResourceView<ChunkSpaceVertex> spaceVertexMap,
                    out MappedResourceView<ChunkPaintVertex> paintVertexMap);

                var indexMapView = new MappedResourceView<uint>(indexMap);
                mesh.Indices.CopyTo(indexMapView.AsSpan());

                mesh.SpaceVertices.CopyTo(spaceVertexMap.AsSpan());
                mesh.PaintVertices.CopyTo(paintVertexMap.AsSpan());
            }
            finally
            {
                stagingMesh.Unmap(gd);
                mesh.Dispose();
            }

            stagingMesh.DrawCount = 0;
            stagingMesh.IndexCount = indexCount;
            stagingMesh.VertexCount = spaceVertexBytesRequired / Unsafe.SizeOf<ChunkSpaceVertex>();

            stagingMesh.Upload(
                gd.ResourceFactory,
                uploadList,
                ref _indexBuffer!,
                ref _spaceVertexBuffer,
                ref _paintVertexBuffer);

            _indexCount = stagingMesh.IndexCount;
            _vertexCount = stagingMesh.VertexCount;
            _uploadRequired = false;
            return true;
        }

        public override void Render(CommandList cl)
        {
            if (_indexCount != 0)
                return;

            cl.SetGraphicsResourceSet(2, _chunkInfoSet);

            cl.SetVertexBuffer(0, _spaceVertexBuffer);
            cl.SetVertexBuffer(1, _paintVertexBuffer);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
        }

        private void DisposeMeshBuffers()
        {
            _indexCount = 0;
            _vertexCount = 0;

            _indexBuffer?.Dispose();
            _indexBuffer = null!;

            _spaceVertexBuffer?.Dispose();
            _spaceVertexBuffer = null!;

            _paintVertexBuffer?.Dispose();
            _paintVertexBuffer = null!;
        }
    }
}
