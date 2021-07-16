using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        private DeviceBuffer _indirectBuffer;
        private DeviceBuffer _renderInfoBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _spaceVertexBuffer;
        private DeviceBuffer _paintVertexBuffer;

        private Stopwatch _buildWatch = new();
        private StoredChunkMesh[] _storedChunks;
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

            _storedChunks = new StoredChunkMesh[size.Volume];
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _uploadRequired = true;

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref _storedChunks[i];
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

            for (int i = 0; i < _storedChunks.Length; i++)
            {
                ref StoredChunkMesh storedChunk = ref _storedChunks[i];
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
                ref StoredChunkMesh storedChunk = ref _storedChunks[i];
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

            NonEmptyStoredChunkEnumerator chunks = new(_storedChunks);
            ChunkUploadResult result = Upload(gd, stagingMeshPool, generateMetaData: true, chunks);
            stagingMesh = result.StagingMesh;
            if (stagingMesh == null)
            {
                if (result.IsEmpty)
                {
                    _indexBuffer?.Dispose();
                    _spaceVertexBuffer?.Dispose();
                    _paintVertexBuffer?.Dispose();

                    DrawCount = 0;
                    _indexCount = 0;
                    _vertexCount = 0;
                    _uploadRequired = false;
                    return true;
                }
                return false;
            }

            ResizeMetaDataBuffers(
                gd.ResourceFactory,
                stagingMesh.MaxChunkCount,
                ref _indirectBuffer,
                ref _renderInfoBuffer);

            ResizeDataBuffers(
                gd.ResourceFactory,
                (uint)result.IndexBytesRequired,
                (uint)result.VertexCount,
                ref _indexBuffer,
                ref _spaceVertexBuffer,
                ref _paintVertexBuffer);

            uint srcOffset = 0;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _indirectBuffer, 0, (uint)result.IndirectBytesRequired);
            srcOffset += (uint)result.IndirectBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _renderInfoBuffer, 0, (uint)result.RenderInfoBytesRequired);
            srcOffset += (uint)result.RenderInfoBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _indexBuffer, 0, (uint)result.IndexBytesRequired);
            srcOffset += (uint)result.IndexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _spaceVertexBuffer, 0, (uint)result.SpaceVertexBytesRequired);
            srcOffset += (uint)result.SpaceVertexBytesRequired;

            uploadList.CopyBuffer(stagingMesh.Buffer, srcOffset, _paintVertexBuffer, 0, (uint)result.PaintVertexBytesRequired);
            srcOffset += (uint)result.PaintVertexBytesRequired;

            DrawCount = result.DrawCount;
            _indexCount = result.IndexCount;
            _vertexCount = result.VertexCount;
            _uploadRequired = false;
            return true;
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
                    ref readonly ChunkMeshResult mesh = ref storedChunk.StoredMesh;
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

                    storedChunk.IsUploadRequired = false;
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
            if (DrawCount == 0 || _indirectBuffer == null || _indexBuffer == null)
                return;

            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, _spaceVertexBuffer);
            cl.SetVertexBuffer(1, _paintVertexBuffer);
            cl.SetVertexBuffer(2, _renderInfoBuffer);
            cl.DrawIndexedIndirect(_indirectBuffer, 0, (uint)DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }

        public static void ResizeMetaDataBuffers(
            ResourceFactory factory,
            uint chunkCount,
            ref DeviceBuffer indirectBuffer,
            ref DeviceBuffer renderInfoBuffer)
        {
            uint indirectSizeInBytes = (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>() * chunkCount;
            if (indirectBuffer == null || indirectBuffer.SizeInBytes < indirectSizeInBytes)
            {
                indirectBuffer?.Dispose();
                indirectBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = indirectSizeInBytes,
                    Usage = BufferUsage.IndirectBuffer,
                });
            }

            uint renderInfoSizeInBytes = (uint)Unsafe.SizeOf<ChunkRenderInfo>() * chunkCount;
            if (renderInfoBuffer == null || renderInfoBuffer.SizeInBytes < renderInfoSizeInBytes)
            {
                renderInfoBuffer?.Dispose();
                renderInfoBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = renderInfoSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }
        }

        public static void ResizeDataBuffers(
            ResourceFactory factory,
            uint indexByteCount,
            uint vertexCount,
            ref DeviceBuffer indexBuffer,
            ref DeviceBuffer spaceVertexBuffer,
            ref DeviceBuffer paintVertexBuffer)
        {
            uint indexSizeInBytes = indexByteCount;
            //if (indexBuffer == null || indexBuffer.SizeInBytes < indexSizeInBytes)
            {
                indexBuffer?.Dispose();
                indexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = indexSizeInBytes,
                    Usage = BufferUsage.IndexBuffer,
                });
            }

            uint spaceVertexSizeInBytes = (uint)(vertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            //if (spaceVertexBuffer == null || spaceVertexBuffer.SizeInBytes < spaceVertexSizeInBytes)
            {
                spaceVertexBuffer?.Dispose();
                spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = spaceVertexSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }

            uint paintVertexSizeInBytes = (uint)(vertexCount * Unsafe.SizeOf<ChunkPaintVertex>());
            //if (paintVertexBuffer == null || paintVertexBuffer.SizeInBytes < paintVertexSizeInBytes)
            {
                paintVertexBuffer?.Dispose();
                paintVertexBuffer = factory.CreateBuffer(new BufferDescription()
                {
                    SizeInBytes = paintVertexSizeInBytes,
                    Usage = BufferUsage.VertexBuffer,
                });
            }
        }
    }
}
