using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkMeshRegion : ChunkMeshBase
    {
        private DeviceBuffer _indirectBuffer;
        private DeviceBuffer _renderInfoBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;

        private Stopwatch _buildWatch = new();
        private StoredChunk[] _storedChunks;
        private int _buildRequired;
        private bool _uploadRequired;
        private int _indexCount;
        private int _vertexCount;

        public ChunkRenderer Renderer { get; }
        public RenderRegionPosition Position { get; }
        public Size3 Size { get; }

        public int DrawCount { get; private set; }

        public override int IndexCount => _indexCount;
        public override int VertexCount => _vertexCount;

        public override bool IsBuildRequired => _buildRequired > 0;
        public override bool IsUploadRequired => _uploadRequired;

        public int RegionX => Position.X;
        public int RegionY => Position.Y;
        public int RegionZ => Position.Z;

        public ChunkMeshRegion(ChunkRenderer chunkRenderer, RenderRegionPosition position, Size3 size)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
            Size = size;

            _storedChunks = new StoredChunk[size.Volume];
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _uploadRequired = true;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (!storedChunk.HasValue)
                    continue;

                storedChunk.IsUploadRequired = true;
            }
        }

        public override void DestroyDeviceObjects()
        {
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

        public ChunkPosition GetLocalChunkPosition(ChunkPosition position)
        {
            return new ChunkPosition(
                (int)((uint)position.X % Size.W),
                (int)((uint)position.Y % Size.H),
                (int)((uint)position.Z % Size.D));
        }

        private int GetStoredChunkIndex(ChunkPosition localPosition)
        {
            return (localPosition.Y * (int)Size.D + localPosition.Z) * (int)Size.W + localPosition.X;
        }

        private ref StoredChunk GetStoredChunk(ChunkPosition localPosition)
        {
            int index = GetStoredChunkIndex(localPosition);
            return ref _storedChunks[index];
        }

        public override void RequestBuild(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);

            ref StoredChunk storedChunk = ref GetStoredChunk(localPosition);
            if (!storedChunk.HasValue)
            {
                storedChunk = new StoredChunk(position, localPosition);
            }

            //Chunk? frontChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 0, chunk.Z + 1);
            //Chunk? backChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 0, chunk.Z - 1);
            //Chunk? topChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 1, chunk.Z + 0);
            //Chunk? bottomChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y - 1, chunk.Z + 0);
            //Chunk? leftChunk = Renderer.GetChunk(chunk.X - 1, chunk.Y + 0, chunk.Z + 0);
            //Chunk? rightChunk = Renderer.GetChunk(chunk.X + 1, chunk.Y + 0, chunk.Z + 0);

            Interlocked.Increment(ref storedChunk.IsBuildRequired);
            Interlocked.Increment(ref _buildRequired);
        }

        public override (int Total, int ToBuild, int ToUpload) GetMeshCount()
        {
            int total = 0;
            int toBuild = 0;
            int toUpload = 0;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (storedChunk.HasValue)
                    total++;

                if (storedChunk.IsBuildRequired > 0)
                    toBuild++;

                if (storedChunk.IsUploadRequired)
                    toUpload++;
            }

            return (total, toBuild, toUpload);
        }

        public override bool Build(ChunkMesher mesher, BlockMemory blockMemoryBuffer)
        {
            int buildRequired = _buildRequired;
            if (buildRequired <= 0)
            {
                return false;
            }

            int c = 0;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (!storedChunk.HasValue)
                    continue;

                storedChunk.IsUploadRequired = true;
                _uploadRequired = true;

                int chunkBuildRequired = storedChunk.IsBuildRequired;
                if (storedChunk.IsBuildRequired == 0)
                    continue;

                storedChunk.StoredMesh.Dispose();

                Renderer.FetchBlockMemory(blockMemoryBuffer, storedChunk.Position.ToBlock());

                _buildWatch.Start();

                ChunkMeshResult result = mesher.Mesh(blockMemoryBuffer);

                _buildWatch.Stop();
                c++;

                storedChunk.StoredMesh = result;
                Interlocked.Add(ref storedChunk.IsBuildRequired, -chunkBuildRequired);
            }

            //if (c != 0)
            //    Console.WriteLine((w.Elapsed.TotalMilliseconds / c).ToString("0.0000") + "ms per mesh");

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

            int chunksToUpload = 0;
            int maxVertexCount = 0;
            int indexCountRequired = 0;
            int spaceVertexBytesRequired = 0;
            int paintVertexBytesRequired = 0;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (!storedChunk.IsUploadRequired)
                    continue;

                ref ChunkMeshResult mesh = ref storedChunk.StoredMesh;

                chunksToUpload++;
                indexCountRequired += mesh.IndexCount;
                spaceVertexBytesRequired += mesh.SpaceVertexByteCount;
                paintVertexBytesRequired += mesh.PaintVertexByteCount;

                int vertexCount = mesh.VertexCount;
                if (vertexCount > maxVertexCount)
                    maxVertexCount = vertexCount;
            }

            if (indexCountRequired <= 0)
            {
                _indexBuffer?.Dispose();
                _spaceVertexBuffer?.Dispose();
                _paintVertexBuffer?.Dispose();

                DrawCount = 0;
                _indexCount = 0;
                _vertexCount = 0;
                _uploadRequired = false;
                stagingMesh = null;
                return true;
            }

            // TODO: rent based on required bytes

            if (!stagingMeshPool.TryRent(
                out stagingMesh,
                chunksToUpload))
            {
                return false;
            }

            uint drawIndex = 0;
            uint indexOffset = 0;
            int vertexOffset = 0;

            try
            {
                stagingMesh.MapIndirect(
                    gd,
                    out MappedResourceView<IndirectDrawIndexedArguments> indirectMap,
                    out MappedResourceView<ChunkRenderInfo> renderInfoMap,
                    out MappedResource indexMap,
                    out MappedResourceView<ChunkSpaceVertex> spaceVertexMap,
                    out MappedResourceView<ChunkPaintVertex> paintVertexMap);

                for (int i = 0; i < _storedChunks.Length; i++)
                {
                    ref StoredChunk storedChunk = ref _storedChunks[i];
                    if (!storedChunk.IsUploadRequired)
                        continue;

                    ref ChunkMeshResult mesh = ref storedChunk.StoredMesh;

                    IndirectDrawIndexedArguments indirectArgs = new()
                    {
                        FirstIndex = indexOffset,
                        FirstInstance = drawIndex,
                        InstanceCount = 1,
                        VertexOffset = vertexOffset,
                        IndexCount = (uint)mesh.IndexCount,
                    };
                    indirectMap[drawIndex] = indirectArgs;

                    renderInfoMap[drawIndex] = storedChunk.RenderInfo;

                    var indexMapView = new MappedResourceView<uint>(indexMap);
                    mesh.Indices.CopyTo(indexMapView.AsSpan(indexOffset));

                    mesh.SpaceVertices.CopyTo(spaceVertexMap.AsSpan(vertexOffset));
                    mesh.PaintVertices.CopyTo(paintVertexMap.AsSpan(vertexOffset));

                    drawIndex++;
                    indexOffset += indirectArgs.IndexCount;
                    vertexOffset += mesh.VertexCount;

                    storedChunk.IsUploadRequired = false;
                }
            }
            finally
            {
                stagingMesh.UnmapIndirect(gd);
            }

            stagingMesh.DrawCount = (int)drawIndex;
            stagingMesh.IndexCount = (int)indexOffset;
            stagingMesh.VertexCount = vertexOffset;

            stagingMesh.Upload(
                gd.ResourceFactory,
                uploadList,
                ref _indirectBuffer,
                ref _renderInfoBuffer,
                ref _indexBuffer,
                ref _spaceVertexBuffer,
                ref _paintVertexBuffer);

            DrawCount = stagingMesh.DrawCount;
            _indexCount = stagingMesh.IndexCount;
            _vertexCount = stagingMesh.VertexCount;
            _uploadRequired = false;
            return true;
        }

        public override void Render(CommandList cl)
        {
            if (DrawCount == 0 || _indirectBuffer == null || _indexBuffer == null)
                return;

            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, _spaceVertexBuffer);
            cl.SetVertexBuffer(1, _paintVertexBuffer);
            cl.SetVertexBuffer(2, _renderInfoBuffer);
            cl.DrawIndexedIndirect(_indirectBuffer, 0, (uint)DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }
    }
}
