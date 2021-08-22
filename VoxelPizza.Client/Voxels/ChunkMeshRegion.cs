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

    public partial class ChunkMeshRegion : ChunkMeshBase
    {
        struct MeshBuffers
        {
            public DeviceBuffer _indirectBuffer;
            public DeviceBuffer _renderInfoBuffer;
            public DeviceBuffer _indexBuffer;
            public DeviceBuffer _spaceVertexBuffer;
            public DeviceBuffer _paintVertexBuffer;

            public int DrawCount;
            public int IndexCount;
            public int VertexCount;

            public void Dispose()
            {
                DrawCount = 0;
                IndexCount = 0;
                VertexCount = 0;

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
        }

        private MeshBuffers _currentMesh;
        private Queue<MeshBuffers> _meshesInTransfer = new Queue<MeshBuffers>();
        private Queue<MeshBuffers> _meshesForUpload = new Queue<MeshBuffers>();
        private readonly object _uploadMutex = new object();

        private Stopwatch _buildWatch = new();
        private StoredChunkMesh[] _storedChunks;
        private int _buildRequired;
        private bool _uploadRequired;

        public ChunkRenderer Renderer { get; }
        public RenderRegionPosition Position { get; }
        public Size3 Size { get; }

        public int DrawCount => _currentMesh.DrawCount;

        public override int IndexCount => _currentMesh.IndexCount;
        public override int VertexCount => _currentMesh.VertexCount;

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

            _storedChunks = new StoredChunkMesh[size.Volume];
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _uploadRequired = true;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref _storedChunks[i];
                if (!storedChunk.HasValue)
                {
                    continue;
                }
                storedChunk.IsUploadRequired = true;
            }
        }

        public override void DestroyDeviceObjects()
        {
            lock (_uploadMutex)
            {
                _currentMesh.Dispose();

                while (_meshesInTransfer.TryDequeue(out MeshBuffers meshBuffers))
                {
                    meshBuffers.Dispose();
                }
                while (_meshesForUpload.TryDequeue(out MeshBuffers meshBuffers))
                {
                    meshBuffers.Dispose();
                }
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

        public override void RequestBuild(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);

            ref StoredChunkMesh storedChunk = ref GetStoredChunk(localPosition);
            if (!storedChunk.HasValue)
            {
                storedChunk = new StoredChunkMesh(position, localPosition);
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

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                if (storedChunk.HasValue)
                {
                    total++;

                    if (storedChunk.IsBuildRequired > 0)
                        toBuild++;

                    if (storedChunk.IsUploadRequired)
                        toUpload++;
                }
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

            bool needsUpload = false;

            StoredChunkMesh[] storedChunks = _storedChunks;
            for (int i = 0; i < storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref storedChunks[i];
                if (!storedChunk.HasValue)
                {
                    continue;
                }
                storedChunk.IsUploadRequired = true;
                needsUpload = true;

                int chunkBuildRequired = storedChunk.IsBuildRequired;
                if (storedChunk.IsBuildRequired == 0)
                {
                    continue;
                }
                storedChunk.StoredMesh.Dispose();

                Renderer.FetchBlockMemory(blockMemoryBuffer, storedChunk.Position.ToBlock());

                _buildWatch.Start();

                ChunkMeshResult result = mesher.Mesh(blockMemoryBuffer);

                _buildWatch.Stop();
                c++;

                storedChunk.StoredMesh = result;
                Interlocked.Add(ref storedChunk.IsBuildRequired, -chunkBuildRequired);
            }

            if (needsUpload)
            {
                _uploadRequired = true;
            }

            //if (c != 0)
            //    Console.WriteLine((w.Elapsed.TotalMilliseconds / c).ToString("0.0000") + "ms per mesh");

            Interlocked.Add(ref _buildRequired, -buildRequired);
            return true;
        }

        private static Stopwatch mapWatch = new Stopwatch();

        public override unsafe bool Upload(
            GraphicsDevice gd, CommandList uploadList, ChunkStagingMeshPool stagingMeshPool,
            out ChunkStagingMesh? stagingMesh)
        {
            if (!_uploadRequired)
            {
                stagingMesh = null;
                return true;
            }

            StoredChunksToUploadEnumerator meshes = new(_storedChunks);
            ChunkUploadResult result = Upload(gd, stagingMeshPool, generateMetaData: true, meshes);
            stagingMesh = result.StagingMesh;
            if (stagingMesh == null)
            {
                if (result.IsEmpty)
                {
                    lock (_uploadMutex)
                    {
                        // Enqueue an empty mesh to clear the current mesh.
                        _meshesForUpload.Enqueue(default);
                        _uploadRequired = false;
                    }
                    return true;
                }
                return false;
            }
            stagingMesh.Owner = this;

            MeshBuffers mesh;

            var factory = gd.ResourceFactory;

            uint indirectSizeInBytes = (uint)(Unsafe.SizeOf<IndirectDrawIndexedArguments>() * result.DrawCount);

            mesh._indirectBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indirectSizeInBytes,
                Usage = BufferUsage.IndirectBuffer,
            });

            uint renderInfoSizeInBytes = (uint)(Unsafe.SizeOf<ChunkRenderInfo>() * result.DrawCount);
            mesh._renderInfoBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = renderInfoSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint indexSizeInBytes = (uint)result.IndexBytesRequired;
            mesh._indexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indexSizeInBytes,
                Usage = BufferUsage.IndexBuffer,
            });

            uint spaceVertexSizeInBytes = (uint)(result.VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            mesh._spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = spaceVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint paintVertexSizeInBytes = (uint)(result.VertexCount * Unsafe.SizeOf<ChunkPaintVertex>());
            mesh._paintVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = paintVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint srcOffset = 0;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, mesh._indirectBuffer, 0, (uint)result.IndirectBytesRequired);
            srcOffset += (uint)result.IndirectBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, mesh._renderInfoBuffer, 0, (uint)result.RenderInfoBytesRequired);
            srcOffset += (uint)result.RenderInfoBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, mesh._indexBuffer, 0, (uint)result.IndexBytesRequired);
            srcOffset += (uint)result.IndexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, mesh._spaceVertexBuffer, 0, (uint)result.SpaceVertexBytesRequired);
            srcOffset += (uint)result.SpaceVertexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, mesh._paintVertexBuffer, 0, (uint)result.PaintVertexBytesRequired);
            srcOffset += (uint)result.PaintVertexBytesRequired;

            mesh.DrawCount = result.DrawCount;
            mesh.IndexCount = result.IndexCount;
            mesh.VertexCount = result.VertexCount;

            _uploadRequired = false;

            lock (_uploadMutex)
            {
                _meshesInTransfer.Enqueue(mesh);
            }
            return true;
        }

        public override void UploadFinished()
        {
            lock (_uploadMutex)
            {
                MeshBuffers transferredBuffers = _meshesInTransfer.Dequeue();
                _meshesForUpload.Enqueue(transferredBuffers);
            }
        }

        public static unsafe ChunkUploadResult Upload<TMeshes>(
            GraphicsDevice gd,
            ChunkStagingMeshPool stagingMeshPool,
            bool generateMetaData,
            TMeshes meshes)
            where TMeshes : IRefEnumerator<StoredChunkMesh>
        {
            int indirectBytesRequired = 0;
            int renderInfoBytesRequired = 0;
            int indexBytesRequired = 0;
            int spaceVertexBytesRequired = 0;
            int paintVertexBytesRequired = 0;

            TMeshes meshesToIterate = meshes;
            while (meshesToIterate.MoveNext())
            {
                ref readonly ChunkMeshResult mesh = ref meshesToIterate.Current.StoredMesh;
                if (mesh.IsEmpty)
                {
                    continue;
                }

                if (generateMetaData)
                {
                    indirectBytesRequired += Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                    renderInfoBytesRequired += Unsafe.SizeOf<ChunkRenderInfo>();
                }

                indexBytesRequired += mesh.IndexByteCount;
                spaceVertexBytesRequired += mesh.SpaceVertexByteCount;
                paintVertexBytesRequired += mesh.PaintVertexByteCount;
            }

            if (indexBytesRequired <= 0)
            {
                TMeshes meshesToClear = meshes;
                while (meshesToClear.MoveNext())
                {
                    ref StoredChunkMesh storedChunk = ref meshesToClear.Current;
                    storedChunk.IsUploadRequired = false;
                }
                return default;
            }

            uint totalBytesRequired =
                (uint)indirectBytesRequired +
                (uint)renderInfoBytesRequired +
                (uint)indexBytesRequired +
                (uint)spaceVertexBytesRequired +
                (uint)paintVertexBytesRequired;

            if (!stagingMeshPool.TryRent(out ChunkStagingMesh? stagingMesh, totalBytesRequired))
            {
                goto ReturnStatus;
            }

            uint drawIndex = 0;
            uint indexOffset = 0;
            int vertexOffset = 0;

            try
            {
                //mapWatch.Start();
                var bufferMap = gd.Map(stagingMesh.Buffer, 0, totalBytesRequired, MapMode.Write, 0);
                //mapWatch.Stop();
                //Console.WriteLine(mapWatch.Elapsed.TotalMilliseconds.ToString("0.0"));

                byte* indirectBytePtr = (byte*)bufferMap.Data;
                byte* renderInfoBytePtr = indirectBytePtr + indirectBytesRequired;
                byte* indexBytePtr = renderInfoBytePtr + renderInfoBytesRequired;
                byte* spaceVertexBytePtr = indexBytePtr + indexBytesRequired;
                byte* paintVertexBytePtr = spaceVertexBytePtr + spaceVertexBytesRequired;

                TMeshes meshesToUpload = meshes;
                while (meshesToUpload.MoveNext())
                {
                    ref StoredChunkMesh storedChunk = ref meshesToUpload.Current;
                    storedChunk.IsUploadRequired = false;

                    ref readonly ChunkMeshResult mesh = ref storedChunk.StoredMesh;
                    if (mesh.IsEmpty)
                    {
                        continue;
                    }

                    uint indexCount = (uint)mesh.IndexCount;

                    if (generateMetaData)
                    {
                        IndirectDrawIndexedArguments indirectArgs = new()
                        {
                            FirstIndex = indexOffset,
                            FirstInstance = drawIndex,
                            InstanceCount = 1,
                            VertexOffset = vertexOffset,
                            IndexCount = indexCount,
                        };

                        uint indirectByteOffset = drawIndex * (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                        Unsafe.Write(indirectBytePtr + indirectByteOffset, indirectArgs);

                        uint renderInfoByteOffset = drawIndex * (uint)Unsafe.SizeOf<ChunkRenderInfo>();
                        Unsafe.Write(renderInfoBytePtr + renderInfoByteOffset, storedChunk.RenderInfo);
                    }

                    uint indexByteOffset = indexOffset * sizeof(uint);
                    Span<byte> indexBytes = MemoryMarshal.AsBytes(mesh.Indices);
                    indexBytes.CopyTo(new Span<byte>(indexBytePtr + indexByteOffset, indexBytes.Length));

                    uint spaceVertexByteOffset = (uint)(vertexOffset * Unsafe.SizeOf<ChunkSpaceVertex>());
                    Span<byte> spaceVertexBytes = MemoryMarshal.AsBytes(mesh.SpaceVertices);
                    spaceVertexBytes.CopyTo(new Span<byte>(spaceVertexBytePtr + spaceVertexByteOffset, spaceVertexBytes.Length));

                    uint paintVertexByteOffset = (uint)(vertexOffset * Unsafe.SizeOf<ChunkPaintVertex>());
                    Span<byte> paintVertexBytes = MemoryMarshal.AsBytes(mesh.PaintVertices);
                    paintVertexBytes.CopyTo(new Span<byte>(paintVertexBytePtr + paintVertexByteOffset, paintVertexBytes.Length));

                    drawIndex++;
                    indexOffset += indexCount;
                    vertexOffset += mesh.VertexCount;
                }
            }
            finally
            {
                gd.Unmap(stagingMesh.Buffer);
            }

            ReturnStatus:
            return new ChunkUploadResult(
                stagingMesh,
                indexBytesRequired / sizeof(uint),
                indirectBytesRequired,
                renderInfoBytesRequired,
                indexBytesRequired,
                spaceVertexBytesRequired,
                paintVertexBytesRequired);
        }

        public override void Render(CommandList cl)
        {
            lock (_uploadMutex)
            {
                while (_meshesForUpload.TryDequeue(out MeshBuffers pendingBuffers))
                {
                    _currentMesh.Dispose();

                    _currentMesh = pendingBuffers;
                }
            }
            DrawMesh(cl, _currentMesh);
        }

        private static void DrawMesh(CommandList cl, in MeshBuffers mesh)
        {
            if (mesh.DrawCount == 0 || mesh._indirectBuffer == null || mesh._indexBuffer == null)
            {
                return;
            }

            cl.SetIndexBuffer(mesh._indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, mesh._spaceVertexBuffer);
            cl.SetVertexBuffer(1, mesh._paintVertexBuffer);
            cl.SetVertexBuffer(2, mesh._renderInfoBuffer);
            cl.DrawIndexedIndirect(mesh._indirectBuffer, 0, (uint)mesh.DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }
    }
}
