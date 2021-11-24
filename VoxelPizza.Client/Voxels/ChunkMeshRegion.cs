using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Collections;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    // TODO: smart checks for reallocations and
    //       separate commonly updated chunks into singular mesh instances

    public partial class ChunkMeshRegion : GraphicsResource
    {
        private ChunkMeshBuffers? _currentMesh;
        private Queue<ChunkMeshBuffers?> _meshesForUpload = new(1);
        private readonly object _uploadMutex = new();

        private Stopwatch _buildWatch = new();
        private StoredChunkMesh[] _storedChunks;
        private int _buildRequired;
        private int _uploadRequired;
        private int _removeRequired;
        private int _chunkCount;

        public ChunkRenderer Renderer { get; }
        public RenderRegionPosition Position { get; private set; }
        public Size3 Size { get; }

        public object WorkerMutex { get; }

        public uint DrawCount => _currentMesh?.DrawCount ?? 0;
        public uint IndexCount => _currentMesh?.IndexCount ?? 0;
        public uint VertexCount => _currentMesh?.VertexCount ?? 0;

        public bool IsBuildRequired => _buildRequired > 0;
        public bool IsUploadRequired => _uploadRequired > 0;
        public bool IsRemoveRequired => _removeRequired > 0;
        public int ChunkCount => _chunkCount;

        public int RegionX => Position.X;
        public int RegionY => Position.Y;
        public int RegionZ => Position.Z;

        public ChunkMeshRegion(ChunkRenderer chunkRenderer, Size3 size)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));
            Size = size;

            WorkerMutex = new object();

            _storedChunks = new StoredChunkMesh[size.Volume];
        }

        public void SetPosition(RenderRegionPosition position)
        {
            Position = position;
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            Interlocked.Increment(ref _uploadRequired);
        }

        public override void DestroyDeviceObjects()
        {
            lock (_uploadMutex)
            {
                EmptyPendingUploads();

                _currentMesh?.Dispose();
                _currentMesh = default;

                //GraphicsDevice.Log($"Destroyed region mesh @{Position}\n");
            }
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

        private ref StoredChunkMesh GetStoredChunk(ChunkPosition localPosition)
        {
            int index = GetStoredChunkIndex(localPosition);
            return ref _storedChunks[index];
        }

        public void ChunkAdded(ChunkPosition chunkPosition)
        {
            Interlocked.Increment(ref _chunkCount);
        }

        public void ChunkRemoved(ChunkPosition chunkPosition)
        {
            Interlocked.Decrement(ref _chunkCount);
        }

        public void AllowBuild(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref StoredChunkMesh storedChunk = ref GetStoredChunk(localPosition);
            if (!storedChunk.HasValue)
            {
                storedChunk = new StoredChunkMesh(chunkPosition, localPosition);
            }

            Interlocked.Increment(ref storedChunk.IsBuildRequired);
            Interlocked.Increment(ref _buildRequired);
        }

        public void RequestBuild(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref StoredChunkMesh storedChunk = ref GetStoredChunk(localPosition);
            if (!storedChunk.HasValue)
            {
                storedChunk = new StoredChunkMesh(chunkPosition, localPosition);
            }

            Interlocked.Increment(ref storedChunk.IsBuildRequired);
            Interlocked.Increment(ref _buildRequired);
        }

        public void RequestRemove(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref StoredChunkMesh storedChunk = ref GetStoredChunk(localPosition);
            if (!storedChunk.HasValue)
            {
                return;
            }

            Interlocked.Increment(ref storedChunk.IsRemoveRequired);
            Interlocked.Increment(ref storedChunk.IsBuildRequired);
            Interlocked.Increment(ref _removeRequired);
            Interlocked.Increment(ref _buildRequired);
        }

        public (int Total, int ToBuild, int ToUpload) GetMeshCount()
        {
            int total = 0;
            int toBuild = 0;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                if (storedChunk.HasValue)
                {
                    total++;

                    if (storedChunk.IsBuildRequired > 0)
                        toBuild++;
                }
            }

            return (total, toBuild, 0);
        }

        public (int ChunkCount, int ToBuild, int ToUpload, int ToRemove) GetCounts()
        {
            return (_chunkCount, _buildRequired > 0 ? 1 : 0, _uploadRequired, _removeRequired);
        }

        public void Reset()
        {
            _removeRequired = default;
            _buildRequired = default;
            _uploadRequired = default;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                storedChunk.StoredMesh.Dispose();
                storedChunk = default;
            }

            DestroyDeviceObjects();
        }

        public bool Cleanup()
        {
            int removeRequested = Interlocked.Exchange(ref _removeRequired, 0);
            if (removeRequested <= 0)
            {
                return false;
            }

            int emptyCount = 0;
            bool uploadRequired = false;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                if (!storedChunk.HasValue)
                {
                    emptyCount++;
                    continue;
                }
                uploadRequired = true;

                int chunkRemoveRequired = Interlocked.Exchange(ref storedChunk.IsRemoveRequired, 0);
                if (chunkRemoveRequired == 0)
                    continue;

                storedChunk.StoredMesh.Dispose();
                storedChunk = default;
                emptyCount++;
            }

            if (emptyCount == storedChunks.Length)
            {
                lock (_uploadMutex)
                {
                    //EmptyPendingTransfers();
                    EmptyPendingUploads();

                    // Enqueue an empty mesh to clear the current mesh.
                    _meshesForUpload.Enqueue(default);

                    uploadRequired = false;
                }
            }

            if (uploadRequired)
            {
                Interlocked.Increment(ref _uploadRequired);
            }
            return true;
        }

        public (bool Built, bool Empty) Build(ChunkMesher mesher, BlockMemory blockBuffer)
        {
            int buildRequired = Interlocked.Exchange(ref _buildRequired, 0);
            if (buildRequired <= 0)
            {
                return (false, false);
            }

            int builtCount = 0;
            int emptyCount = 0;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                if (!storedChunk.HasValue)
                {
                    emptyCount++;
                    continue;
                }

                int chunkBuildRequired = Interlocked.Exchange(ref storedChunk.IsBuildRequired, 0);
                if (chunkBuildRequired != 0)
                {
                    storedChunk.StoredMesh.Dispose();

                    int chunkRemoveRequired = Interlocked.Exchange(ref storedChunk.IsRemoveRequired, 0);
                    if (chunkRemoveRequired == 0)
                    {
                        Renderer.FetchBlockMemory(blockBuffer, storedChunk.Position.ToBlock());

                        _buildWatch.Start();
                        storedChunk.StoredMesh = mesher.Mesh(blockBuffer);
                        _buildWatch.Stop();

                        builtCount++;
                    }
                }

                if (storedChunk.StoredMesh.IsEmpty)
                {
                    emptyCount++;
                }
            }

            //if (c != 0)
            //    Console.WriteLine((w.Elapsed.TotalMilliseconds / c).ToString("0.0000") + "ms per mesh");

            return (builtCount > 0, emptyCount == storedChunks.Length);
        }

        private static Stopwatch mapWatch = new Stopwatch();

        public ChunkMeshSizes GetMeshSizes()
        {
            return GetMeshSizes(_storedChunks);
        }

        public ChunkMeshBuffers Copy(
            GraphicsDevice graphicsDevice,
            CommandList uploadList,
            ChunkMeshSizes sizes,
            DeviceBuffer sourceBuffer,
            uint sourceOffset)
        {
            ResourceFactory factory = graphicsDevice.ResourceFactory;

            ChunkMeshBuffers mesh = new();
            mesh.SyncPoint = Stopwatch.GetTimestamp();

            uint indirectSizeInBytes = (uint)(Unsafe.SizeOf<IndirectDrawIndexedArguments>() * sizes.DrawCount);
            mesh._indirectBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indirectSizeInBytes,
                Usage = BufferUsage.IndirectBuffer,
            });

            uint renderInfoSizeInBytes = (uint)(Unsafe.SizeOf<ChunkRenderInfo>() * sizes.DrawCount);
            mesh._renderInfoBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = renderInfoSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint indexSizeInBytes = sizes.IndexBytesRequired;
            mesh._indexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indexSizeInBytes,
                Usage = BufferUsage.IndexBuffer,
            });

            uint spaceVertexSizeInBytes = (uint)(sizes.VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            mesh._spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = spaceVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint paintVertexSizeInBytes = (uint)(sizes.VertexCount * Unsafe.SizeOf<ChunkPaintVertex>());
            mesh._paintVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = paintVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            if (graphicsDevice.IsDebug)
            {
                mesh._indirectBuffer.Name = $"{nameof(ChunkMeshRegion)}@{Position}.{nameof(mesh._indirectBuffer)}";
                mesh._renderInfoBuffer.Name = $"{nameof(ChunkMeshRegion)}@{Position}.{nameof(mesh._renderInfoBuffer)}";
                mesh._indexBuffer.Name = $"{nameof(ChunkMeshRegion)}@{Position}.{nameof(mesh._indexBuffer)}";
                mesh._spaceVertexBuffer.Name = $"{nameof(ChunkMeshRegion)}@{Position}.{nameof(mesh._spaceVertexBuffer)}";
                mesh._paintVertexBuffer.Name = $"{nameof(ChunkMeshRegion)}@{Position}.{nameof(mesh._paintVertexBuffer)}";
            }

            bool debugMarkers = graphicsDevice.Features.CommandListDebugMarkers;
            if (debugMarkers)
            {
                uploadList.PushDebugGroup($"Upload of {nameof(ChunkMeshRegion)}@{Position}");
            }

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._indirectBuffer, 0, sizes.IndirectBytesRequired);
            sourceOffset += sizes.IndirectBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._renderInfoBuffer, 0, sizes.RenderInfoBytesRequired);
            sourceOffset += sizes.RenderInfoBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._indexBuffer, 0, sizes.IndexBytesRequired);
            sourceOffset += sizes.IndexBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._spaceVertexBuffer, 0, sizes.SpaceVertexBytesRequired);
            sourceOffset += sizes.SpaceVertexBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._paintVertexBuffer, 0, sizes.PaintVertexBytesRequired);
            sourceOffset += sizes.PaintVertexBytesRequired;

            if (debugMarkers)
            {
                uploadList.PopDebugGroup();
            }

            mesh.DrawCount = sizes.DrawCount;
            mesh.IndexCount = sizes.IndexCount;
            mesh.VertexCount = sizes.VertexCount;

            return mesh;
        }

        private void EmptyPendingUploads()
        {
            while (_meshesForUpload.TryDequeue(out ChunkMeshBuffers? pendingBuffers))
            {
                pendingBuffers?.Dispose();
            }
        }

        public void UploadFinished(ChunkMeshBuffers? transferredBuffers)
        {
            if (IsDisposed)
            {
                transferredBuffers?.Dispose();
                return;
            }

            lock (_uploadMutex)
            {
                EmptyPendingUploads();

                _meshesForUpload.Enqueue(transferredBuffers);
            }
        }

        public static unsafe ChunkMeshSizes GetMeshSizes(ReadOnlySpan<StoredChunkMesh> storedChunks)
        {
            uint indexCount = 0;
            uint indirectBytesRequired = 0;
            uint renderInfoBytesRequired = 0;
            uint indexBytesRequired = 0;
            uint spaceVertexBytesRequired = 0;
            uint paintVertexBytesRequired = 0;

            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref readonly ChunkMeshResult mesh = ref storedChunks[i].StoredMesh;
                if (mesh.IsEmpty)
                {
                    continue;
                }

                indirectBytesRequired += (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                renderInfoBytesRequired += (uint)Unsafe.SizeOf<ChunkRenderInfo>();

                indexCount += mesh.IndexCount;
                indexBytesRequired += mesh.IndexByteCount;
                spaceVertexBytesRequired += mesh.SpaceVertexByteCount;
                paintVertexBytesRequired += mesh.PaintVertexByteCount;
            }

            return new ChunkMeshSizes(
                indexCount,
                indirectBytesRequired,
                renderInfoBytesRequired,
                indexBytesRequired,
                spaceVertexBytesRequired,
                paintVertexBytesRequired);
        }

        public unsafe void WriteMeshes(
            ChunkMeshSizes sizes,
            byte* destination)
        {
            uint drawIndex = 0;
            uint indexOffset = 0;
            uint vertexOffset = 0;

            byte* indirectBytePtr = destination;
            byte* renderInfoBytePtr = indirectBytePtr + sizes.IndirectBytesRequired;
            byte* indexBytePtr = renderInfoBytePtr + sizes.RenderInfoBytesRequired;
            byte* spaceVertexBytePtr = indexBytePtr + sizes.IndexBytesRequired;
            byte* paintVertexBytePtr = spaceVertexBytePtr + sizes.SpaceVertexBytesRequired;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                ref readonly ChunkMeshResult mesh = ref storedChunk.StoredMesh;
                if (mesh.IsEmpty)
                {
                    continue;
                }

                uint indexCount = mesh.IndexCount;

                IndirectDrawIndexedArguments indirectArgs = new()
                {
                    FirstIndex = indexOffset,
                    FirstInstance = drawIndex,
                    InstanceCount = 1,
                    VertexOffset = (int)vertexOffset,
                    IndexCount = indexCount,
                };

                uint indirectByteOffset = drawIndex * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                Unsafe.Write(indirectBytePtr + indirectByteOffset, indirectArgs);

                uint renderInfoByteOffset = drawIndex * (uint)Unsafe.SizeOf<ChunkRenderInfo>();
                Unsafe.Write(renderInfoBytePtr + renderInfoByteOffset, storedChunk.RenderInfo);

                uint indexByteOffset = indexOffset * sizeof(uint);
                Span<byte> indexBytes = MemoryMarshal.AsBytes(mesh.Indices);
                indexBytes.CopyTo(new Span<byte>(indexBytePtr + indexByteOffset, indexBytes.Length));

                uint spaceVertexByteOffset = vertexOffset * (uint)Unsafe.SizeOf<ChunkSpaceVertex>();
                Span<byte> spaceVertexBytes = MemoryMarshal.AsBytes(mesh.SpaceVertices);
                spaceVertexBytes.CopyTo(new Span<byte>(spaceVertexBytePtr + spaceVertexByteOffset, spaceVertexBytes.Length));

                uint paintVertexByteOffset = vertexOffset * (uint)Unsafe.SizeOf<ChunkPaintVertex>();
                Span<byte> paintVertexBytes = MemoryMarshal.AsBytes(mesh.PaintVertices);
                paintVertexBytes.CopyTo(new Span<byte>(paintVertexBytePtr + paintVertexByteOffset, paintVertexBytes.Length));

                drawIndex++;
                indexOffset += indexCount;
                vertexOffset += mesh.VertexCount;
            }
        }

        public void Render(CommandList cl)
        {
            lock (_uploadMutex)
            {
                // We should never have more than one pending mesh to upload.
                while (_meshesForUpload.TryDequeue(out ChunkMeshBuffers? pendingBuffers))
                {
                    _currentMesh?.Dispose();
                    _currentMesh = pendingBuffers;
                }
            }

            ChunkMeshBuffers? currentMesh = _currentMesh;
            if (currentMesh != null)
            {
                DrawMesh(cl, currentMesh);
            }
        }

        private static void DrawMesh(CommandList cl, ChunkMeshBuffers mesh)
        {
            cl.SetIndexBuffer(mesh._indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, mesh._spaceVertexBuffer);
            cl.SetVertexBuffer(1, mesh._paintVertexBuffer);
            cl.SetVertexBuffer(2, mesh._renderInfoBuffer);
            cl.DrawIndexedIndirect(mesh._indirectBuffer, 0, mesh.DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }
    }
}
