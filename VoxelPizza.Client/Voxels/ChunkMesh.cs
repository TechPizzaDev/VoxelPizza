using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Collections;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkMesh : ChunkMeshBase
    {
        private ResourceSet _chunkInfoSet;

        private StoredChunkMesh _mesh;
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

        public override bool IsBuildRequired => _mesh.IsBuildRequired > 0;
        public override bool IsUploadRequired => _mesh.IsUploadRequired;

        public ChunkMesh(ChunkRenderer chunkRenderer, ChunkPosition position)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _mesh.IsUploadRequired = true;

            ResourceFactory factory = gd.ResourceFactory;

            _renderInfoBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkRenderInfo>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            var chunkInfo = new ChunkRenderInfo(new Vector4(
                Position.X * Chunk.Width,
                Position.Y * Chunk.Height,
                Position.Z * Chunk.Depth,
                0));

            cl.UpdateBuffer(_renderInfoBuffer, 0, ref chunkInfo);

            _chunkInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                Renderer.ChunkInfoLayout,
                _renderInfoBuffer));
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
            return (1, _mesh.IsBuildRequired != 0 ? 1 : 0, _mesh.IsUploadRequired ? 1 : 0);
        }

        public override void RequestBuild(ChunkPosition position)
        {
            Debug.Assert(position == Position);

            RequestBuild();
        }

        private void RequestBuild()
        {
            Interlocked.Increment(ref _mesh.IsBuildRequired);
        }

        public override bool Build(ChunkMesher mesher, BlockMemory blockMemoryBuffer)
        {
            int buildRequired = _mesh.IsBuildRequired;
            if (buildRequired <= 0)
            {
                return false;
            }

            _mesh.StoredMesh.Dispose();

            Renderer.FetchBlockMemory(blockMemoryBuffer, Position.ToBlock());

            _mesh.StoredMesh = Renderer.ChunkMesher.Mesh(blockMemoryBuffer);

            _mesh.IsUploadRequired = true;

            Interlocked.Add(ref _mesh.IsBuildRequired, -buildRequired);
            return true;
        }

        public override bool Upload(
            GraphicsDevice gd, CommandList uploadList, ChunkStagingMeshPool stagingMeshPool,
            out ChunkStagingMesh? stagingMesh)
        {
            if (!_mesh.IsUploadRequired)
            {
                stagingMesh = null;
                return true;
            }

            SingleNonEmptyStoredChunkEnumerator chunks = new(this);
            ChunkUploadResult result = ChunkMeshRegion.Upload(gd, stagingMeshPool, generateMetaData: false, chunks);
            stagingMesh = result.StagingMesh;
            if (stagingMesh == null)
            {
                if (result.IsEmpty)
                {
                    DisposeMeshBuffers();

                    _mesh.IsUploadRequired = false;
                    return true;
                }
                return false;
            }

            ChunkMeshRegion.ResizeDataBuffers(
                gd.ResourceFactory,
                (uint)result.IndexBytesRequired,
                (uint)result.VertexCount,
                ref _indexBuffer,
                ref _spaceVertexBuffer,
                ref _paintVertexBuffer);

            uint srcOffset = 0;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _indexBuffer, 0, (uint)result.IndexBytesRequired);
            srcOffset += (uint)result.IndexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _spaceVertexBuffer, 0, (uint)result.SpaceVertexBytesRequired);
            srcOffset += (uint)result.SpaceVertexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _paintVertexBuffer, 0, (uint)result.PaintVertexBytesRequired);
            srcOffset += (uint)result.PaintVertexBytesRequired;

            _indexCount = result.IndexCount;
            _vertexCount = result.VertexCount;
            _mesh.IsUploadRequired = false;
            return true;
        }

        public override void Render(CommandList cl)
        {
            if (_indexBuffer == null)
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

        private struct SingleNonEmptyStoredChunkEnumerator : IRefEnumerator<StoredChunkMesh>
        {
            private ChunkMesh _mesh;
            private bool _move;

            public ref StoredChunkMesh Current => ref _mesh._mesh;

            public SingleNonEmptyStoredChunkEnumerator(ChunkMesh mesh)
            {
                _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
                _move = false;
            }

            public bool MoveNext()
            {
                if (!_move)
                {
                    _move = true;
                    ref StoredChunkMesh chunk = ref _mesh._mesh;
                    return chunk.IsUploadRequired && !chunk.StoredMesh.IsEmpty;
                }
                return false;
            }
        }
    }
}
