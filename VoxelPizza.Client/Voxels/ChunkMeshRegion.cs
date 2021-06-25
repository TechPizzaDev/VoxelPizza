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
    public partial class ChunkMeshRegion : GraphicsResource
    {
        private DeviceBuffer _indirectBuffer;
        private DeviceBuffer _translationBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;

        private Stopwatch _buildWatch = new Stopwatch();
        private StoredChunk[] _storedChunks;
        private List<ChunkPosition> _chunksToUpload;
        private int _buildRequired;
        private bool _uploadRequired;

        public ChunkRenderer Renderer { get; }
        public ChunkRegionPosition Position { get; }
        public Size3 Size { get; }

        public int DrawCount { get; private set; }
        public int IndexCount { get; private set; }
        public int VertexCount { get; private set; }

        public bool BuildRequired => _buildRequired > 0;
        public bool IsUploadRequired => _uploadRequired;

        public int RegionX => Position.X;
        public int RegionY => Position.Y;
        public int RegionZ => Position.Z;

        public ChunkMeshRegion(ChunkRenderer chunkRenderer, ChunkRegionPosition position, Size3 size)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
            Size = size;

            _storedChunks = new StoredChunk[size.Volume];
            _chunksToUpload = new();
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _uploadRequired = true;
        }

        public override void DestroyDeviceObjects()
        {
            _indirectBuffer?.Dispose();
            _indirectBuffer = null!;
            _translationBuffer?.Dispose();
            _translationBuffer = null!;
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

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);
            ref StoredChunk storedChunk = ref GetStoredChunk(localPosition);
            if (storedChunk.HasValue)
                return storedChunk.Chunk;
            return null;
        }

        public void UpdateChunk(Chunk chunk)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunk.Position);
            ref StoredChunk storedChunk = ref GetStoredChunk(localPosition);

            if (!storedChunk.HasValue)
                storedChunk = new StoredChunk(chunk, localPosition);

            //Chunk? frontChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 0, chunk.Z + 1);
            //Chunk? backChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 0, chunk.Z - 1);
            //Chunk? topChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y + 1, chunk.Z + 0);
            //Chunk? bottomChunk = Renderer.GetChunk(chunk.X + 0, chunk.Y - 1, chunk.Z + 0);
            //Chunk? leftChunk = Renderer.GetChunk(chunk.X - 1, chunk.Y + 0, chunk.Z + 0);
            //Chunk? rightChunk = Renderer.GetChunk(chunk.X + 1, chunk.Y + 0, chunk.Z + 0);

            storedChunk.IsDirty = true;
            Interlocked.Increment(ref _buildRequired);
        }

        public int GetPendingChunkCount()
        {
            int dirtyChunkCount = 0;
            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (storedChunk.HasValue && storedChunk.IsDirty)
                    dirtyChunkCount++;
            }
            return dirtyChunkCount;
        }

        public int GetChunkCount()
        {
            int chunkCount = 0;
            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (storedChunk.HasValue)
                    chunkCount++;
            }
            return chunkCount;
        }

        public bool Build(ChunkMesher mesher)
        {
            int buildRequired = _buildRequired;
            if (buildRequired <= 0)
                return false;

            BlockMemory? blockMemory = null;
            int c = 0;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (!storedChunk.HasValue)
                    continue;

                if (!storedChunk.IsDirty)
                    continue;

                storedChunk.StoredMesh.Dispose();

                //Chunk chunk = storedChunk.Chunk;
                //Chunk? frontChunk = Renderer.GetChunk(chunk.X, chunk.Y, chunk.Z + 1);
                //Chunk? backChunk = Renderer.GetChunk(chunk.X, chunk.Y, chunk.Z - 1);
                //Chunk? topChunk = Renderer.GetChunk(chunk.X, chunk.Y + 1, chunk.Z);
                //Chunk? bottomChunk = Renderer.GetChunk(chunk.X, chunk.Y - 1, chunk.Z);
                //Chunk? leftChunk = Renderer.GetChunk(chunk.X - 1, chunk.Y, chunk.Z);
                //Chunk? rightChunk = Renderer.GetChunk(chunk.X + 1, chunk.Y, chunk.Z);

                if (blockMemory == null)
                {
                    blockMemory = new BlockMemory(
                        Renderer.GetBlockMemoryInnerSize(),
                        Renderer.GetBlockMemoryOuterSize());
                }

                Renderer.FetchBlockMemory(blockMemory, storedChunk.Chunk.Position.ToBlock());

                _buildWatch.Start();

                ChunkMeshResult result = mesher.Mesh(blockMemory);

                _buildWatch.Stop();
                c++;

                storedChunk.StoredMesh = result;

                storedChunk.IsDirty = false;

                _uploadRequired = true;
            }

            //if (c != 0)
            //    Console.WriteLine((w.Elapsed.TotalMilliseconds / c).ToString("0.0000") + "ms per mesh");

            Interlocked.Add(ref _buildRequired, -buildRequired);
            return true;
        }

        public ChunkStagingMesh? Upload(GraphicsDevice gd, CommandList uploadList, ChunkStagingMeshPool stagingMeshPool)
        {
            if (!_uploadRequired)
                return null;

            int maxVertexCount = 0;
            int indexCountRequired = 0;
            int spaceVertexBytesRequired = 0;
            int paintVertexBytesRequired = 0;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunk storedChunk = ref _storedChunks[i];
                if (!storedChunk.HasValue)
                    continue;

                ref ChunkMeshResult mesh = ref storedChunk.StoredMesh;

                int indexCount = mesh.IndexCount;
                if (indexCount > 0)
                {
                    _chunksToUpload.Add(storedChunk.LocalPosition);

                    indexCountRequired += indexCount;
                    spaceVertexBytesRequired += mesh.SpaceVertexByteCount;
                    paintVertexBytesRequired += mesh.PaintVertexByteCount;

                    int vertexCount = mesh.VertexCount;
                    if (vertexCount > maxVertexCount)
                        maxVertexCount = vertexCount;
                }
            }

            if (indexCountRequired <= 0)
            {
                return null;
            }

            // TODO: rent based on required bytes

            if (!stagingMeshPool.TryRent(
                out ChunkStagingMesh? stagingMesh,
                _chunksToUpload.Count))
            {
                _chunksToUpload.Clear();
                return null;
            }

            uint drawIndex;
            uint indexOffset = 0;
            int vertexOffset = 0;

            try
            {
                stagingMesh.Map(
                    gd,
                    out MappedResourceView<IndirectDrawIndexedArguments> indirectMap,
                    out MappedResourceView<ChunkInfo> translationMap,
                    out MappedResource indexMap,
                    out MappedResourceView<ChunkSpaceVertex> spaceVertexMap,
                    out MappedResourceView<ChunkPaintVertex> paintVertexMap);

                for (drawIndex = 0; drawIndex < _chunksToUpload.Count; drawIndex++)
                {
                    ChunkPosition lpos = _chunksToUpload[(int)drawIndex];
                    ref StoredChunk storedChunk = ref GetStoredChunk(lpos);
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

                    translationMap[drawIndex] = storedChunk.ChunkInfo;

                    var indexMapView = new MappedResourceView<uint>(indexMap);
                    mesh.Indices.CopyTo(indexMapView.AsSpan(indexOffset));

                    mesh.SpaceVertices.CopyTo(spaceVertexMap.AsSpan(vertexOffset));
                    mesh.PaintVertices.CopyTo(paintVertexMap.AsSpan(vertexOffset));

                    indexOffset += indirectArgs.IndexCount;
                    vertexOffset += mesh.VertexCount;
                }
            }
            finally
            {
                stagingMesh.Unmap(gd);
                _chunksToUpload.Clear();
            }

            stagingMesh.DrawCount = (int)drawIndex;
            stagingMesh.IndexCount = (int)indexOffset;
            stagingMesh.VertexCount = vertexOffset;

            stagingMesh.Upload(
                gd.ResourceFactory,
                uploadList,
                ref _indirectBuffer,
                ref _translationBuffer,
                ref _indexBuffer,
                ref _spaceVertexBuffer,
                ref _paintVertexBuffer);

            DrawCount = stagingMesh.DrawCount;
            IndexCount = stagingMesh.IndexCount;
            VertexCount = stagingMesh.VertexCount;
            _uploadRequired = false;

            return stagingMesh;
        }

        public void Render(CommandList cl)
        {
            if (DrawCount == 0 || _indirectBuffer == null || _indexBuffer == null)
                return;

            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, _spaceVertexBuffer);
            cl.SetVertexBuffer(1, _paintVertexBuffer);
            cl.SetVertexBuffer(2, _translationBuffer);
            cl.DrawIndexedIndirect(_indirectBuffer, 0, (uint)DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }
    }
}
