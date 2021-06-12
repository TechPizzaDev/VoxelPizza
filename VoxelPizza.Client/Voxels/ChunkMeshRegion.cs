using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkMeshRegion : GraphicsResource
    {
        private class StoredChunk
        {
            public Chunk Chunk { get; }
            public ChunkInfo ChunkInfo;

            public bool IsDirty;
            public ChunkMeshResult StoredMesh;

            public StoredChunk(Chunk chunk)
            {
                Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));

                ChunkInfo = new ChunkInfo
                {
                    Translation = new Vector3(
                        chunk.X * Chunk.Width,
                        chunk.Y * Chunk.Height,
                        chunk.Z * Chunk.Depth)
                };
            }
        }

        private DeviceBuffer _indirectBuffer;
        private DeviceBuffer _translationBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;

        private StoredChunk?[,,] _chunks;
        private List<StoredChunk> _chunksToUpload;
        private int _buildRequired;
        private bool _uploadRequired;

        public ChunkRenderer Renderer { get; }
        public ChunkRegionPosition Position { get; }
        public UInt3 Size { get; }

        public int DrawCount { get; private set; }
        public int IndexCount { get; private set; }
        public int VertexCount { get; private set; }

        public bool BuildRequired => _buildRequired > 0;
        public bool IsUploadRequired => _uploadRequired;

        public int RegionX => Position.X;
        public int RegionY => Position.Y;
        public int RegionZ => Position.Z;
        public uint Volume => Size.X * Size.Y * Size.Z;

        public ChunkMeshRegion(ChunkRenderer chunkRenderer, ChunkRegionPosition position, UInt3 size)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
            Size = size;

            _chunks = new StoredChunk[size.Y, size.Z, size.X];
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
                (int)((uint)position.X % Size.X),
                (int)((uint)position.Y % Size.Y),
                (int)((uint)position.Z % Size.Z));
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkPosition lposition = GetLocalChunkPosition(position);
            ref StoredChunk? storedChunk = ref _chunks[lposition.Y, lposition.Z, lposition.X];
            return storedChunk?.Chunk;
        }

        public void UpdateChunk(Chunk chunk)
        {
            ChunkPosition lpos = GetLocalChunkPosition(chunk.Position);
            ref StoredChunk? storedChunk = ref _chunks[lpos.Y, lpos.Z, lpos.X];

            if (storedChunk == null)
                storedChunk = new StoredChunk(chunk);

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
            int sum = 0;
            for (int y = 0; y < _chunks.GetLength(0); y++)
            {
                for (int z = 0; z < _chunks.GetLength(1); z++)
                {
                    for (int x = 0; x < _chunks.GetLength(2); x++)
                    {
                        StoredChunk? storedChunk = _chunks[y, z, x];
                        if (storedChunk == null)
                            continue;

                        if (storedChunk.IsDirty)
                            sum++;
                    }
                }
            }
            return sum;
        }

        public int GetChunkCount()
        {
            int sum = 0;
            for (int y = 0; y < _chunks.GetLength(0); y++)
            {
                for (int z = 0; z < _chunks.GetLength(1); z++)
                {
                    for (int x = 0; x < _chunks.GetLength(2); x++)
                    {
                        if (_chunks[y, z, x] != null)
                            sum++;
                    }
                }
            }
            return sum;
        }

        public bool Build(ChunkMesher mesher)
        {
            int buildRequired = _buildRequired;
            if (buildRequired <= 0)
                return false;

            BlockMemory? blockMemory = null;
            Stopwatch w = new Stopwatch();
            int c = 0;

            for (int y = 0; y < _chunks.GetLength(0); y++)
            {
                for (int z = 0; z < _chunks.GetLength(1); z++)
                {
                    for (int x = 0; x < _chunks.GetLength(2); x++)
                    {
                        StoredChunk? storedChunk = _chunks[y, z, x];
                        if (storedChunk == null)
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

                        w.Start();

                        ChunkMeshResult result = mesher.Mesh(blockMemory);

                        w.Stop();
                        c++;

                        storedChunk.StoredMesh = result;

                        storedChunk.IsDirty = false;

                        _uploadRequired = true;
                    }
                }
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

            int totalIndexCount = 0;
            int totalVertexCount = 0;

            for (int y = 0; y < _chunks.GetLength(0); y++)
            {
                for (int z = 0; z < _chunks.GetLength(1); z++)
                {
                    for (int x = 0; x < _chunks.GetLength(2); x++)
                    {
                        StoredChunk? storedChunk = _chunks[y, z, x];
                        if (storedChunk == null)
                            continue;

                        int indexCount = storedChunk.StoredMesh.IndexCount;
                        if (indexCount > 0)
                        {
                            _chunksToUpload.Add(storedChunk);

                            totalIndexCount += indexCount;
                            totalVertexCount += storedChunk.StoredMesh.VertexCount;
                        }
                    }
                }
            }

            if (totalIndexCount <= 0)
            {
                return null;
            }

            int indexBytesRequired = 0;
            int spaceBytesRequired = 0;
            int paintBytesRequired = 0;

            for (int i = 0; i < _chunksToUpload.Count; i++)
            {
                StoredChunk storedChunk = _chunksToUpload[(int)i];
                ref ChunkMeshResult mesh = ref storedChunk.StoredMesh;

                indexBytesRequired += mesh.IndexCount * sizeof(uint);
                spaceBytesRequired += mesh.VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>();
                paintBytesRequired += mesh.VertexCount * Unsafe.SizeOf<ChunkPaintVertex>();
            }

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
                    StoredChunk storedChunk = _chunksToUpload[(int)drawIndex];
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

        public void Render(GraphicsDevice gd, CommandList cl)
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
