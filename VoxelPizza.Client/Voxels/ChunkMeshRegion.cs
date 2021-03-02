using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkMeshRegion : GraphicsResource
    {
        private class StoredChunk
        {
            public Chunk Chunk { get; }
            public ChunkInfo ChunkInfo;

            public bool IsDirty;
            public StoredChunkMesh StoredMesh;

            public StoredChunk(Chunk chunk)
            {
                Chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));

                ChunkInfo = new ChunkInfo
                {
                    Translation = new Vector3(chunk.ChunkX * 16, chunk.ChunkY * 16, chunk.ChunkZ * 16)
                };
            }
        }

        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;
        private DeviceBuffer _translationBuffer;
        private DeviceBuffer _indirectBuffer;

        private StoredChunk?[,,] _chunks;
        private int _dirtyChunkCount;

        public ChunkRenderer Renderer { get; }
        public ChunkRegionPosition Position { get; }
        public Int3 Size { get; }

        public uint DrawCount { get; private set; }
        public uint TriangleCount { get; private set; }

        public int RegionX => Position.X;
        public int RegionY => Position.Y;
        public int RegionZ => Position.Z;
        public int Volume => Size.X * Size.Y * Size.Z;

        public ChunkMeshRegion(ChunkRenderer chunkRenderer, ChunkRegionPosition position, Int3 size)
        {
            if (size.IsNegative())
                throw new ArgumentOutOfRangeException(nameof(size));

            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Position = position;
            Size = size;

            _chunks = new StoredChunk[size.Y, size.Z, size.X];
        }

        // int regionX, int regionY, int regionZ new ChunkRegionPosition(regionX, regionY, regionZ)

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _indirectBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>() * (uint)Volume, BufferUsage.IndirectBuffer));

            _translationBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<ChunkInfo>() * (uint)Volume, BufferUsage.VertexBuffer));

            Interlocked.Add(ref _dirtyChunkCount, Volume);
        }

        public override void DestroyDeviceObjects()
        {
            _indexBuffer?.Dispose();
            _indexBuffer = null!;
            _spaceVertexBuffer?.Dispose();
            _spaceVertexBuffer = null!;
            _paintVertexBuffer?.Dispose();
            _paintVertexBuffer = null!;
            _translationBuffer?.Dispose();
            _translationBuffer = null!;

            _indirectBuffer?.Dispose();
        }

        public ChunkPosition GetLocalChunkPosition(ChunkPosition position)
        {
            return new ChunkPosition(
                position.X % Size.X,
                position.Y % Size.Y,
                position.Z % Size.Z);
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkPosition lposition = GetLocalChunkPosition(position);
            ref StoredChunk? storedChunk = ref _chunks[lposition.Y, lposition.Z, lposition.X];
            return storedChunk?.Chunk;
        }

        public void UpdateChunk(Chunk chunk)
        {
            ChunkPosition lposition = GetLocalChunkPosition(chunk.Position);
            ref StoredChunk? storedChunk = ref _chunks[lposition.Y, lposition.Z, lposition.X];

            if (storedChunk == null)
                storedChunk = new StoredChunk(chunk);

            storedChunk.IsDirty = true;
            Interlocked.Increment(ref _dirtyChunkCount);
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

        public int Build(GraphicsDevice gd, CommandList bufferList, ref int allowedUpdates)
        {
            int bufferUpdates = 0;

            int dirtyChunkCount = _dirtyChunkCount;
            if (dirtyChunkCount <= 0 || allowedUpdates <= 0 || _indirectBuffer == null)
                return bufferUpdates;

            ChunkMesher mesher = new ChunkMesher(ArrayPool<byte>.Shared);
            List<(int x, int y, int z)> renderCoords = new List<(int x, int y, int z)>();

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

                        if (storedChunk.IsDirty)
                        {
                            //storedChunk.StoredMesh.Dispose();

                            ChunkMeshResult result = mesher.Mesh(storedChunk.Chunk);
                            storedChunk.StoredMesh = new StoredChunkMesh(result);

                            storedChunk.IsDirty = false;
                        }

                        totalIndexCount += storedChunk.StoredMesh.Indices.Count;
                        totalVertexCount += storedChunk.StoredMesh.SpaceVertices.Count;
                        renderCoords.Add((x, y, z));
                    }
                }
            }

            uint indexSizeInBytes = (uint)totalIndexCount * sizeof(uint);
            if (_indexBuffer == null || _indexBuffer.SizeInBytes < indexSizeInBytes)
            {
                _indexBuffer?.Dispose();

                if (indexSizeInBytes != 0)
                {
                    _indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = indexSizeInBytes,
                        Usage = BufferUsage.IndexBuffer,
                    });
                }
            }

            uint spaceVertexSizeInBytes = (uint)(totalVertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            if (_spaceVertexBuffer == null || _spaceVertexBuffer.SizeInBytes < spaceVertexSizeInBytes)
            {
                _spaceVertexBuffer?.Dispose();
                _paintVertexBuffer?.Dispose();

                if (_indexBuffer != null)
                {
                    _spaceVertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = spaceVertexSizeInBytes,
                        Usage = BufferUsage.VertexBuffer,
                    });

                    _paintVertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription()
                    {
                        SizeInBytes = (uint)(totalVertexCount * Unsafe.SizeOf<ChunkPaintVertex>()),
                        Usage = BufferUsage.VertexBuffer,
                    });
                }
            }

            DrawCount = 0;
            uint indexOffset = 0;
            int vertexOffset = 0;

            foreach (var (x, y, z) in renderCoords)
            {
                StoredChunk? storedChunk = _chunks[y, z, x];
                Debug.Assert(storedChunk != null);

                ref StoredChunkMesh mesh = ref storedChunk.StoredMesh;

                uint indexCount = (uint)mesh.Indices.Count;
                int vertexCount = mesh.SpaceVertices.Count;

                if (indexCount == 0)
                    continue;

                IndirectDrawIndexedArguments indirectArgs = new()
                {
                    FirstIndex = indexOffset,
                    FirstInstance = DrawCount,
                    InstanceCount = 1,
                    VertexOffset = vertexOffset,
                    IndexCount = indexCount,
                };

                bufferList.UpdateBuffer(
                    _indexBuffer, indirectArgs.FirstIndex * sizeof(uint), (ReadOnlySpan<uint>)mesh.Indices.Span);

                bufferList.UpdateBuffer(
                    _spaceVertexBuffer, (uint)(indirectArgs.VertexOffset * Unsafe.SizeOf<ChunkSpaceVertex>()),
                    (ReadOnlySpan<ChunkSpaceVertex>)mesh.SpaceVertices.Span);

                bufferList.UpdateBuffer(
                    _paintVertexBuffer, (uint)(indirectArgs.VertexOffset * Unsafe.SizeOf<ChunkPaintVertex>()),
                    (ReadOnlySpan<ChunkPaintVertex>)mesh.PaintVertices.Span);

                bufferList.UpdateBuffer(_indirectBuffer, DrawCount * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>(), ref indirectArgs);

                bufferList.UpdateBuffer(_translationBuffer, DrawCount * (uint)Unsafe.SizeOf<ChunkInfo>(), storedChunk.ChunkInfo);

                indexOffset += indexCount;
                vertexOffset += vertexCount;
                DrawCount++;

                bufferUpdates += 5;
                allowedUpdates -= 5;
            }

            TriangleCount = indexOffset / 3;
            Interlocked.Add(ref _dirtyChunkCount, -dirtyChunkCount);

            return bufferUpdates;
        }

        public void Render(GraphicsDevice gd, CommandList cl)
        {
            if (DrawCount == 0 || _indirectBuffer == null || _indexBuffer == null)
                return;

            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, _spaceVertexBuffer);
            cl.SetVertexBuffer(1, _paintVertexBuffer);
            cl.SetVertexBuffer(2, _translationBuffer);
            cl.DrawIndexedIndirect(_indirectBuffer, 0, DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }
    }
}
