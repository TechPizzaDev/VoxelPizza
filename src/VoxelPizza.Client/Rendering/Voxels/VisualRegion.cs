using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class VisualRegion
    {
        private const int IndirectCapacityAlignment = 1024 * 16;
        private const int RenderInfoCapacityAlignment = 1024 * 16;
        private const int IndexCapacityAlignment = 1024 * 64 * 2;
        private const int VertexCapacityAlignment = 1024 * 64 * 4;

        public RenderRegionPosition Position { get; private set; }
        public Size3 Size { get; }

        private VisualRegionChunk[] _storedChunks;

        private bool _isDisposed;
        public ChunkMeshBuffers? _meshBuffers;

        // TODO: 
        // Arena manager - you allocate segments through the manager.
        // The manager contains multiple arenas, and segments know which arena they come from.
        // When an arena gets full, return segments from a new arena.
        // Copy old arena into new arena asynchronously.
        // Generations can also be introduced, so untouched meshes get moved into a compacted arena.

        // TODO: divide into heaps based on region rebuild frequency
        public GraphicsArenaAllocator _indexArena;
        public GraphicsArenaAllocator _vertexArena;
        public GraphicsArenaAllocator _renderInfoArena;
        public GraphicsArenaAllocator _indirectArena;

        private List<ChunkMeshBuffers> _meshBuffersInUse = new();

        public VisualRegion(Size3 size)
        {
            Size = size;

            uint volume = size.Volume;
            _storedChunks = new VisualRegionChunk[volume];
        }

        private void InitializeArenas(ResourceFactory factory)
        {
            Size3 size = Size;
            uint volume = size.Volume;
            uint initialCapacity = size.D * size.W * Chunk.Width * Chunk.Depth;

            uint indexCapacity = AlignCapacity(initialCapacity * 6 * (uint)Unsafe.SizeOf<uint>(), IndexCapacityAlignment);
            _indexArena = GraphicsArenaAllocator.Create(
                factory, indexCapacity, BufferUsage.IndexBuffer);

            uint vertexCapacity = AlignCapacity(initialCapacity * 4 * (uint)Unsafe.SizeOf<ChunkSpaceVertex>(), VertexCapacityAlignment);
            _vertexArena = GraphicsArenaAllocator.Create(
                factory, vertexCapacity, BufferUsage.VertexBuffer);

            uint renderInfoCapacity = AlignCapacity(volume * 4 * (uint)Unsafe.SizeOf<ChunkRenderInfo>(), RenderInfoCapacityAlignment);
            _renderInfoArena = GraphicsArenaAllocator.Create(
                factory, renderInfoCapacity, BufferUsage.VertexBuffer);

            uint indirectCapacity = AlignCapacity(
                volume * 4 * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>(), IndirectCapacityAlignment);
            _indirectArena = GraphicsArenaAllocator.Create(
                factory, indirectCapacity, BufferUsage.IndirectBuffer);
        }

        public void Render(CommandList cl)
        {
            ChunkMeshBuffers? currentMesh = _meshBuffers;
            if (currentMesh != null)
            {
                DrawMesh(cl, currentMesh);
            }
        }

        private static void DrawMesh(CommandList cl, ChunkMeshBuffers mesh)
        {
            if (mesh._indexBuffer == null)
            {
                return;
            }

            cl.SetIndexBuffer(mesh._indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, mesh._vertexBuffer);
            cl.SetVertexBuffer(1, mesh._renderInfoBuffer, mesh.RenderInfoSegment.Offset);

            cl.DrawIndexedIndirect(
                mesh._indirectBuffer,
                mesh.IndirectSegment.Offset,
                mesh.IndirectCount,
                (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }

        public void SetMeshBuffers(ChunkMeshBuffers? meshBuffers)
        {
            if (meshBuffers != null)
            {
                foreach ((ArenaSegment, ArenaAllocator) segment in meshBuffers.OldIndexSegments)
                    segment.Item2.Free(segment.Item1);

                foreach ((ArenaSegment, ArenaAllocator) segment in meshBuffers.OldVertexSegments)
                    segment.Item2.Free(segment.Item1);

                foreach (DeviceBuffer buffer in meshBuffers.OldBuffers)
                {
                    buffer.Dispose();
                }
                meshBuffers.OldBuffers.Clear();
            }

            if (_isDisposed && meshBuffers != null)
            {
                meshBuffers.Dispose();
                meshBuffers = null;
            }

            if (_meshBuffers != null)
            {
                if (_meshBuffers.RenderInfoSegment.Length != 0)
                    _renderInfoArena.Free(_meshBuffers.RenderInfoSegment);

                if (_meshBuffers.IndirectSegment.Length != 0)
                    _indirectArena.Free(_meshBuffers.IndirectSegment);

                _meshBuffers.Dispose();
                _meshBuffersInUse.Remove(_meshBuffers);
            }

            _meshBuffers = meshBuffers;
        }

        public enum EncodeStatus
        {
            NotEnoughSpace,
            NoChange,
            Incomplete,
            Success,
        }

        private static Span<T> Slice<T>(Span<T> span, int start, int length)
        {
            try
            {
                return span.Slice(start, length);
            }
            catch
            {
                // FIXME: TODO: investigate how this was ever possible
                Debugger.Launch();
                throw;
            }
        }

        public EncodeStatus EncodeV2(
            LogicalRegion logicalRegion,
            DeviceBuffer stagingBuffer,
            Span<byte> stagingDestination,
            uint stagingOffset,
            ResourceFactory factory,
            CommandList cl,
            out ChannelSizes channelSizes,
            out ChunkMeshBuffers? meshBuffers)
        {
            Span<VisualRegionChunk> visualChunks = _storedChunks;
            Span<LogicalRegionChunk> logicalChunks = Slice(logicalRegion._storedChunks.AsSpan(), 0, visualChunks.Length);
            uint destinationSize = (uint)stagingDestination.Length;

            uint minInstanceCount = 0;

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref readonly VisualRegionChunk visualChunk = ref visualChunks[i];

                if (visualChunk.IndexSegment.Length > 0)
                {
                    minInstanceCount++;
                }
            }

            uint minIndirectSize = minInstanceCount * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
            uint minRenderInfoSize = minInstanceCount * (uint)Unsafe.SizeOf<ChunkRenderInfo>();
            uint minTotalSize = minIndirectSize + minRenderInfoSize;

            channelSizes = new ChannelSizes(4)
            {
                IndirectSize = minIndirectSize,
                RenderInfoSize = minRenderInfoSize,
                IndexSize = 0,
                SpaceVertexSize = 0,
            };

            if (minTotalSize >= destinationSize)
            {
                meshBuffers = null;
                return EncodeStatus.NotEnoughSpace;
            }

            uint instanceCount = 0;
            uint totalSize = 0;
            uint indexSize = 0;
            uint vertexSize = 0;
            uint chunkChangeCount = 0;
            int actualChunkCount = visualChunks.Length;
            bool notEnoughSpace = false;

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];
                ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];

                if (logicalChunk.Version != visualChunk.Version)
                {
                    uint indexByteCount = logicalChunk.Mesh.IndexByteCount;
                    if (indexByteCount == 0)
                    {
                        chunkChangeCount++;
                        visualChunk.Version = logicalChunk.Version;

                        visualChunk.NeedsClear = true;
                        continue;
                    }

                    if (!notEnoughSpace)
                    {
                        uint newTotalSize = totalSize;
                        uint vertexByteCount = logicalChunk.Mesh.SpaceVertexByteCount;

                        newTotalSize += (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                        newTotalSize += (uint)Unsafe.SizeOf<ChunkRenderInfo>();
                        newTotalSize += indexByteCount;
                        newTotalSize += vertexByteCount;

                        if (newTotalSize + minTotalSize <= destinationSize)
                        {
                            instanceCount++;
                            totalSize = newTotalSize;
                            indexSize += indexByteCount;
                            vertexSize += vertexByteCount;
                            chunkChangeCount++;
                            visualChunk.Version = logicalChunk.Version;
                            visualChunk.NeedsClear = true;
                            continue;
                        }
                        else
                        {
                            actualChunkCount = i;
                            notEnoughSpace = true;
                        }
                    }
                }

                if (visualChunk.IndexSegment.Length > 0)
                {
                    instanceCount++;
                    totalSize += (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                    totalSize += (uint)Unsafe.SizeOf<ChunkRenderInfo>();
                }
            }

            uint indirectSize = instanceCount * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
            uint renderInfoSize = instanceCount * (uint)Unsafe.SizeOf<ChunkRenderInfo>();

            channelSizes = new ChannelSizes(4)
            {
                IndirectSize = indirectSize,
                RenderInfoSize = renderInfoSize,
                IndexSize = indexSize,
                SpaceVertexSize = vertexSize,
            };

            Debug.Assert(totalSize == channelSizes.TotalSize);
            Debug.Assert(totalSize <= destinationSize);

            if (chunkChangeCount == 0)
            {
                for (int i = 0; i < visualChunks.Length; i++)
                {
                    ref VisualRegionChunk visualChunk = ref visualChunks[i];
                    Debug.Assert(!visualChunk.NeedsClear);
                }

                meshBuffers = null;
                if (actualChunkCount != visualChunks.Length)
                {
                    return EncodeStatus.NotEnoughSpace;
                }
                return EncodeStatus.NoChange;
            }

            ChunkMeshBuffers chunkMeshBuffers = new();

            if (instanceCount == 0)
            {
                for (int i = 0; i < visualChunks.Length; i++)
                {
                    ref VisualRegionChunk visualChunk = ref visualChunks[i];
                    ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];
                    ref readonly ChunkMeshResult logicalMesh = ref logicalChunk.Mesh;

                    ref ArenaSegment indexSegment = ref visualChunk.IndexSegment;
                    ref ArenaSegment vertexSegment = ref visualChunk.VertexSegment;

                    if (visualChunk.NeedsClear)
                    {
                        if (indexSegment.Length > 0)
                        {
                            chunkMeshBuffers.OldIndexSegments.Add((indexSegment, _indexArena.Allocator));
                            indexSegment = default;
                        }

                        if (vertexSegment.Length > 0)
                        {
                            chunkMeshBuffers.OldVertexSegments.Add((vertexSegment, _vertexArena.Allocator));
                            vertexSegment = default;
                        }

                        visualChunk.NeedsClear = false;
                    }
                }

                meshBuffers = chunkMeshBuffers;

                if (actualChunkCount != visualChunks.Length)
                {
                    return EncodeStatus.Incomplete;
                }
                return EncodeStatus.Success;
            }

            if (_indexArena.Buffer == null)
            {
                InitializeArenas(factory);
                Debug.Assert(_indexArena.Buffer != null);
            }

            ArenaSegment AllocSegment<TIn, TTransform>(
                ref GraphicsArenaAllocator arena,
                uint arenaCapacityAlignment,
                Span<TIn> elements,
                uint length,
                uint alignment = 1)
                where TTransform : IIteratorTransform<TIn, ArenaSegment>, new()
            {
                Debug.Assert(length != 0);

                if (arena.Allocator.TryAlloc(length, alignment, out ArenaSegment segment))
                    return segment;

                EnsureCapacityFor<TIn, TTransform>(
                    ref arena, elements, length, arenaCapacityAlignment, chunkMeshBuffers.OldBuffers, factory, cl);

                if (!arena.Allocator.TryAlloc(length, alignment, out segment))
                    throw new Exception("Failed to allocate after resize.");

                return segment;
            }

            uint dstOffset = 0;
            int indirectOffset = 0;
            int renderInfoOffset = 0;
            int indexOffset = 0;
            int vertexOffset = 0;

            Span<ChunkMeshBuffers> meshBuffersInUse = CollectionsMarshal.AsSpan(_meshBuffersInUse);

            ArenaSegment indirectSegment = AllocSegment<ChunkMeshBuffers, IndirectBufferTransform>(
                ref _indirectArena, IndirectCapacityAlignment, meshBuffersInUse, indirectSize);

            ArenaSegment renderInfoSegment = AllocSegment<ChunkMeshBuffers, RenderInfoBufferTransform>(
                ref _renderInfoArena, RenderInfoCapacityAlignment, meshBuffersInUse, renderInfoSize);

            Span<byte> indirectDst = Slice(stagingDestination, (int)dstOffset, (int)indirectSize);
            uint indirectDstOffset = stagingOffset + dstOffset;
            dstOffset += indirectSize;

            Span<byte> renderInfoDst = Slice(stagingDestination, (int)dstOffset, (int)renderInfoSize);
            uint renderInfoDstOffset = stagingOffset + dstOffset;
            dstOffset += renderInfoSize;

            Span<byte> indexDst = Slice(stagingDestination, (int)dstOffset, (int)indexSize);
            uint indexDstOffset = stagingOffset + dstOffset;
            dstOffset += indexSize;

            Span<byte> spaceVertexDst = Slice(stagingDestination, (int)dstOffset, (int)vertexSize);
            uint vertexDstOffset = stagingOffset + dstOffset;
            dstOffset += vertexSize;

            Debug.Assert(dstOffset == totalSize);

            List<int> missedIndexAllocs = new();
            uint extraIndexSize = 0;

            List<int> missedVertexAllocs = new();
            uint extraVertexSize = 0;

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];
                ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];
                ref readonly ChunkMeshResult logicalMesh = ref logicalChunk.Mesh;

                ref ArenaSegment indexSegment = ref visualChunk.IndexSegment;
                ref ArenaSegment vertexSegment = ref visualChunk.VertexSegment;

                if (visualChunk.NeedsClear)
                {
                    if (indexSegment.Length > 0)
                    {
                        chunkMeshBuffers.OldIndexSegments.Add((indexSegment, _indexArena.Allocator));
                        indexSegment = default;
                    }

                    if (vertexSegment.Length > 0)
                    {
                        chunkMeshBuffers.OldVertexSegments.Add((vertexSegment, _vertexArena.Allocator));
                        vertexSegment = default;
                    }

                    visualChunk.NeedsClear = false;
                }

                if (i >= actualChunkCount)
                {
                    continue;
                }

                if (logicalMesh.IsEmpty)
                {
                    continue;
                }

                visualChunk.NeedsUpload = true;

                uint indexByteCount = logicalMesh.IndexByteCount;
                if (!_indexArena.Allocator.TryAlloc(indexByteCount, 1, out indexSegment))
                {
                    extraIndexSize += indexByteCount;
                    missedIndexAllocs.Add(i);
                }

                uint vertexByteCount = logicalMesh.SpaceVertexByteCount;
                if (!_vertexArena.Allocator.TryAlloc(vertexByteCount, 1, out vertexSegment))
                {
                    extraVertexSize += vertexByteCount;
                    missedVertexAllocs.Add(i);
                }
            }

            checked
            {
                // If extraIndexSize is above zero, then we need a compaction.
                if (extraIndexSize > 0 || _indexArena.BytesFree < indexSize)
                {
                    uint freedSize = 0;
                    foreach ((ArenaSegment, ArenaAllocator) oldBuf in chunkMeshBuffers.OldIndexSegments)
                    {
                        freedSize += oldBuf.Item1.Length;
                        oldBuf.Item2.Free(oldBuf.Item1);
                    }
                    chunkMeshBuffers.OldIndexSegments.Clear();

                    uint newCapacity = (uint)Math.Max((int)(indexSize + extraIndexSize) - (int)freedSize, 0);
                    EnsureCapacityFor<VisualRegionChunk, IndexBufferTransform>(
                        ref _indexArena, visualChunks, newCapacity, IndexCapacityAlignment, chunkMeshBuffers.OldBuffers, factory, cl);
                }
                foreach (int i in missedIndexAllocs)
                {
                    uint size = logicalChunks[i].Mesh.IndexByteCount;
                    if (!_indexArena.Allocator.TryAlloc(size, 1, out visualChunks[i].IndexSegment))
                        throw new Exception("Failed to allocate indices after resize.");
                }

                // If extraVertexSize is above zero, then we need a compaction.
                if (extraVertexSize > 0 || _vertexArena.BytesFree < vertexSize)
                {
                    uint freedSize = 0;
                    foreach ((ArenaSegment, ArenaAllocator) oldBuf in chunkMeshBuffers.OldVertexSegments)
                    {
                        freedSize += oldBuf.Item1.Length;
                        oldBuf.Item2.Free(oldBuf.Item1);
                    }
                    chunkMeshBuffers.OldVertexSegments.Clear();

                    uint newCapacity = (uint)Math.Max((int)(vertexSize + extraVertexSize) - (int)freedSize, 0);
                    EnsureCapacityFor<VisualRegionChunk, VertexBufferTransform>(
                        ref _vertexArena, visualChunks, newCapacity, VertexCapacityAlignment, chunkMeshBuffers.OldBuffers, factory, cl);
                }
                foreach (int i in missedVertexAllocs)
                {
                    uint size = logicalChunks[i].Mesh.SpaceVertexByteCount;
                    if (!_vertexArena.Allocator.TryAlloc(size, 1, out visualChunks[i].VertexSegment))
                        throw new Exception("Failed to allocate vertices after resize.");
                }
            }

            uint instanceIndex = 0;
            uint totalIndexCount = 0;
            uint totalVertexCount = 0;
            int chunksToCopy = 0;

            cl.PushDebugGroup($"Uploading segments in region ({Position})");

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];

                ref ArenaSegment indexSegment = ref visualChunk.IndexSegment;
                ref ArenaSegment vertexSegment = ref visualChunk.VertexSegment;

                if (indexSegment.Length == 0 ||
                    vertexSegment.Length == 0)
                {
                    Debug.Assert(vertexSegment.Length == 0);
                    continue;
                }

                if (visualChunk.NeedsUpload)
                {
                    chunksToCopy++;
                }

                {
                    uint segmentIndexOffset = indexSegment.Offset / sizeof(uint);
                    uint segmentIndexCount = indexSegment.Length / sizeof(uint);

                    uint segmentVertexOffset = vertexSegment.Offset / (uint)Unsafe.SizeOf<ChunkSpaceVertex>();
                    uint segmentVertexCount = vertexSegment.Length / (uint)Unsafe.SizeOf<ChunkSpaceVertex>();

                    IndirectDrawIndexedArguments indirect = new()
                    {
                        FirstIndex = segmentIndexOffset,
                        FirstInstance = instanceIndex,
                        InstanceCount = 1,
                        VertexOffset = (int)segmentVertexOffset,
                        IndexCount = segmentIndexCount,
                    };
                    MemoryMarshal.Write(indirectDst[indirectOffset..], indirect);
                    indirectOffset += Unsafe.SizeOf<IndirectDrawIndexedArguments>();

                    instanceIndex++;
                    totalIndexCount += segmentIndexCount;
                    totalVertexCount += segmentVertexCount;
                }

                {
                    ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];

                    ChunkRenderInfo renderInfo = new()
                    {
                        Translation = new Vector4(
                            logicalChunk.Position.X * Chunk.Width,
                            logicalChunk.Position.Y * Chunk.Height,
                            logicalChunk.Position.Z * Chunk.Depth,
                            0)
                    };
                    MemoryMarshal.Write(renderInfoDst[renderInfoOffset..], renderInfo);
                    renderInfoOffset += Unsafe.SizeOf<ChunkRenderInfo>();
                }
            }

            Debug.Assert(indirectOffset == indirectSize);

            List<BufferCopyCommand> copyCommands = new();
            copyCommands.EnsureCapacity(chunksToCopy);

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];
                if (!visualChunk.NeedsUpload)
                {
                    continue;
                }

                ref ArenaSegment segment = ref visualChunk.IndexSegment;
                if (segment.Length == 0)
                {
                    continue;
                }

                ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];
                cl.InsertDebugMarker($"Uploading index data for chunk ({logicalChunk.Position})");

                Span<byte> bytes = logicalChunk.Mesh.Indices.AsBytes();
                Debug.Assert(segment.Length == (uint)bytes.Length);

                bytes.CopyTo(Slice(indexDst, indexOffset, bytes.Length));

                copyCommands.Add(new BufferCopyCommand(
                    indexDstOffset + (uint)indexOffset,
                    segment.Offset,
                    segment.Length));

                indexOffset += bytes.Length;
            }
            cl.CopyBuffer(
                stagingBuffer,
                _indexArena.Buffer,
                CollectionsMarshal.AsSpan(copyCommands));
            copyCommands.Clear();

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];
                if (!visualChunk.NeedsUpload)
                {
                    continue;
                }

                ref ArenaSegment segment = ref visualChunk.VertexSegment;
                if (segment.Length == 0)
                {
                    continue;
                }

                ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];
                cl.InsertDebugMarker($"Uploading vertex data for chunk ({logicalChunk.Position})");

                Span<byte> bytes = logicalChunk.Mesh.SpaceVertices.AsBytes();
                Debug.Assert(segment.Length == (uint)bytes.Length);

                bytes.CopyTo(Slice(spaceVertexDst, vertexOffset, bytes.Length));

                copyCommands.Add(new BufferCopyCommand(
                    vertexDstOffset + (uint)vertexOffset,
                    segment.Offset,
                    segment.Length));

                vertexOffset += bytes.Length;
            }
            cl.CopyBuffer(
                stagingBuffer,
                _vertexArena.Buffer,
                CollectionsMarshal.AsSpan(copyCommands));
            copyCommands.Clear();

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref VisualRegionChunk visualChunk = ref visualChunks[i];
                if (!visualChunk.NeedsUpload)
                {
                    continue;
                }
                visualChunk.NeedsUpload = false;

                ref LogicalRegionChunk logicalChunk = ref logicalChunks[i];
                logicalRegion.BytesForMesh -= LogicalRegion.GetBytesForMesh(logicalChunk.Mesh);
                logicalChunk.Mesh.Dispose();
            }

            cl.InsertDebugMarker("Uploading draw calls");
            cl.CopyBuffer(stagingBuffer, indirectDstOffset, _indirectArena.Buffer, indirectSegment.Offset, indirectSegment.Length);
            cl.CopyBuffer(stagingBuffer, renderInfoDstOffset, _renderInfoArena.Buffer, renderInfoSegment.Offset, renderInfoSegment.Length);

            cl.PopDebugGroup();

            chunkMeshBuffers._indirectBuffer = _indirectArena.Buffer;
            chunkMeshBuffers._renderInfoBuffer = _renderInfoArena.Buffer;
            chunkMeshBuffers._indexBuffer = _indexArena.Buffer;
            chunkMeshBuffers._vertexBuffer = _vertexArena.Buffer;

            chunkMeshBuffers.IndirectSegment = indirectSegment;
            chunkMeshBuffers.IndirectCount = instanceIndex;

            chunkMeshBuffers.RenderInfoSegment = renderInfoSegment;

            chunkMeshBuffers.SyncPoint = Stopwatch.GetTimestamp();
            chunkMeshBuffers.IndexCount = totalIndexCount;
            chunkMeshBuffers.VertexCount = totalVertexCount;

            meshBuffers = chunkMeshBuffers;
            _meshBuffersInUse.Add(meshBuffers);

            if (actualChunkCount != visualChunks.Length)
            {
                return EncodeStatus.Incomplete;
            }

            for (int i = 0; i < visualChunks.Length; i++)
            {
                ref readonly VisualRegionChunk visualChunk = ref visualChunks[i];
                ref readonly LogicalRegionChunk logicalChunk = ref logicalChunks[i];

                Debug.Assert(logicalChunk.Version == visualChunk.Version);
                Debug.Assert(logicalChunk.Mesh.IsEmpty);
            }

            return EncodeStatus.Success;
        }

        public void SetPosition(RenderRegionPosition position)
        {
            Position = position;
        }

        public ChunkPosition GetLocalChunkPosition(ChunkPosition chunkPosition)
        {
            return new ChunkPosition(
                (int)((uint)chunkPosition.X % Size.W),
                (int)((uint)chunkPosition.Y % Size.H),
                (int)((uint)chunkPosition.Z % Size.D));
        }

        public void Dispose()
        {
            _isDisposed = true;

            SetMeshBuffers(null);

            if (_indexArena.Buffer != null)
            {
                _indexArena.Buffer.Dispose();
                _vertexArena.Buffer.Dispose();

                _renderInfoArena.Buffer.Dispose();
                _indirectArena.Buffer.Dispose();
            }
        }

        private static uint AlignCapacity(uint capacity, uint alignment)
        {
            uint alignedCapacity = (capacity + alignment - 1) / alignment * alignment;
            return alignedCapacity;
        }

        public static void EnsureCapacityFor<TIn, TTransform>(
            ref GraphicsArenaAllocator allocator,
            Span<TIn> elements,
            uint capacity,
            uint capacityAlignment,
            List<DeviceBuffer> oldBuffers,
            ResourceFactory factory,
            CommandList commandList)
            where TTransform : IIteratorTransform<TIn, ArenaSegment>, new()
        {
            uint free = allocator.BytesFree;
            int required = (int)capacity - (int)free;
            uint newCapacity = Math.Max((uint)((int)allocator.ByteCapacity + required), allocator.ByteCapacity);
            uint alignedCapacity = AlignCapacity(newCapacity, capacityAlignment);

            GraphicsArenaAllocator newAllocator = Resize<TIn, TTransform>(
                allocator, elements, alignedCapacity, factory, commandList);

            //Console.WriteLine(
            //    $"Before: " +
            //    $"{allocator.ByteCapacity / 1024}kB / {allocator.SegmentsFree} " +
            //    $"-> After: " +
            //    $"{newAllocator.ByteCapacity / 1024}kB");

            Debug.Assert(allocator.Buffer != newAllocator.Buffer);
            oldBuffers.Add(allocator.Buffer);

            allocator = newAllocator;
        }

        public static GraphicsArenaAllocator Resize<TIn, TTransform>(
            GraphicsArenaAllocator allocator,
            Span<TIn> elements,
            uint newCapacity,
            ResourceFactory factory,
            CommandList commandList)
            where TTransform : IIteratorTransform<TIn, ArenaSegment>, new()
        {
            List<BufferCopyCommand> copyCommands = new((int)allocator.SegmentsUsed);
            Compact<TIn, TTransform>(elements, 0, copyCommands);

            GraphicsArenaAllocator newAllocator = GraphicsArenaAllocator.Create(factory, newCapacity, allocator.Usage);
            TTransform transform = new();

            foreach (ref TIn usedSegment in elements)
            {
                ref ArenaSegment segment = ref transform.Transform(ref usedSegment, out bool skip);
                if (!skip)
                {
                    ArenaSegment previousSegment = segment;
                    Debug.Assert(previousSegment.Length != 0);

                    if (!newAllocator.TryAlloc(segment.Length, 1, out segment))
                    {
                        throw new Exception($"Failed to allocate after resize. Previous segment: {previousSegment}");
                    }
                }
            }

            {
                commandList.PushDebugGroup($"Arena ({allocator.Usage}): Resize {allocator.ByteCapacity} to {newAllocator.ByteCapacity}");

                commandList.CopyBuffer(allocator.Buffer, newAllocator.Buffer, CollectionsMarshal.AsSpan(copyCommands));

                commandList.PopDebugGroup();
            }
            return newAllocator;
        }

        public readonly struct RenderInfoBufferTransform : IIteratorTransform<ChunkMeshBuffers, ArenaSegment>
        {
            public ref ArenaSegment Transform(ref ChunkMeshBuffers input, out bool skip)
            {
                skip = false;
                return ref input.RenderInfoSegment;
            }
        }

        public readonly struct IndirectBufferTransform : IIteratorTransform<ChunkMeshBuffers, ArenaSegment>
        {
            public ref ArenaSegment Transform(ref ChunkMeshBuffers input, out bool skip)
            {
                skip = false;
                return ref input.IndirectSegment;
            }
        }

        public readonly struct VertexBufferTransform : IIteratorTransform<VisualRegionChunk, ArenaSegment>
        {
            public ref ArenaSegment Transform(ref VisualRegionChunk input, out bool skip)
            {
                skip = input.VertexSegment.Length == 0;
                return ref input.VertexSegment;
            }
        }

        public readonly struct IndexBufferTransform : IIteratorTransform<VisualRegionChunk, ArenaSegment>
        {
            public ref ArenaSegment Transform(ref VisualRegionChunk input, out bool skip)
            {
                skip = input.IndexSegment.Length == 0;
                return ref input.IndexSegment;
            }
        }

        public interface IIteratorTransform<TIn, TOut>
        {
            ref TOut Transform(ref TIn input, out bool skip);
        }

        private static void Compact<TIn, TTransform>(
            Span<TIn> elements,
            uint offset,
            List<BufferCopyCommand> commands)
            where TTransform : IIteratorTransform<TIn, ArenaSegment>, new()
        {
            TTransform transform = new();

            BufferCopyCommand command = default;

            uint writeOffset = offset;

            foreach (ref TIn usedSegment in elements)
            {
                ref ArenaSegment segment = ref transform.Transform(ref usedSegment, out bool skip);
                if (skip)
                {
                    continue;
                }

                if (command.Length == 0 || command.ReadOffset + command.Length != segment.Offset)
                {
                    if (command.Length != 0)
                        commands.Add(command);

                    command = new BufferCopyCommand(segment.Offset, writeOffset, segment.Length);
                }
                else
                {
                    command.Length += segment.Length;
                }

                writeOffset += segment.Length;
            }

            if (command.Length != 0)
                commands.Add(command);
        }
    }

    public struct VisualRegionChunk
    {
        public ArenaSegment IndexSegment;
        public ArenaSegment VertexSegment;

        public ushort Version;
        public bool NeedsClear;
        public bool NeedsUpload;
    }

    public unsafe struct ChannelSizes
    {
        public const int MaxChannelCount = 31;

        public fixed uint Sizes[1 + MaxChannelCount];

        public readonly uint ChannelCount => Sizes[0];

        public ref uint IndirectSize => ref Sizes[1];
        public ref uint RenderInfoSize => ref Sizes[2];
        public ref uint IndexSize => ref Sizes[3];
        public ref uint SpaceVertexSize => ref Sizes[4];
        public ref uint PaintVertexSize => ref Sizes[5];

        public ChannelSizes(uint channelCount)
        {
            if (channelCount > MaxChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelCount));
            }
            Sizes[0] = channelCount;
        }

        public readonly uint TotalSize
        {
            get
            {
                uint sum = 0;
                for (int i = 0; i < ChannelCount; i++)
                {
                    sum += Sizes[i + 1];
                }
                return sum;
            }
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < ChannelCount; i++)
                {
                    if (Sizes[i + 1] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
