using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Veldrid;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class VisualRegion
    {
        public RenderRegionPosition Position { get; private set; }
        public Size3 Size { get; }

        private VisualRegionChunk[] _storedChunks;
        private int _updateRequired;

        private bool _isDisposed;
        public ChunkMeshBuffers? _meshBuffers;

        public VisualRegion(Size3 size)
        {
            Size = size;

            _storedChunks = new VisualRegionChunk[size.Volume];
            for (int y = 0; y < size.H; y++)
            {
                for (int z = 0; z < size.D; z++)
                {
                    for (int x = 0; x < size.W; x++)
                    {
                        ChunkPosition localPos = new(x, y, z);
                        _storedChunks[GetStoredChunkIndex(localPos)].LocalPosition = localPos;
                    }
                }
            }
        }

        public void Render(CommandList cl)
        {
            ChunkMeshBuffers? currentMesh = _meshBuffers;
            if (currentMesh != null)
            {
                //draws++;
                if (draws > 10)
                    return;

                DrawMesh(cl, currentMesh);
            }
        }

        int draws = 0;

        private static void DrawMesh(CommandList cl, ChunkMeshBuffers mesh)
        {
            cl.SetIndexBuffer(mesh._indexBuffer, IndexFormat.UInt32);
            cl.SetVertexBuffer(0, mesh._spaceVertexBuffer);
            cl.SetVertexBuffer(1, mesh._paintVertexBuffer);
            cl.SetVertexBuffer(2, mesh._renderInfoBuffer);
            cl.DrawIndexedIndirect(mesh._indirectBuffer, 0, mesh.DrawCount, (uint)Unsafe.SizeOf<IndirectDrawIndexedArguments>());
        }

        public void SetMeshBuffers(ChunkMeshBuffers? meshBuffers)
        {
            draws = 0;

            if (_isDisposed && meshBuffers != null)
            {
                meshBuffers.Dispose();
                meshBuffers = null;
            }

            _meshBuffers?.Dispose();
            _meshBuffers = meshBuffers;
        }

        public bool Encode(LogicalRegion logicalRegion, Span<byte> destination, out ChannelSizes channelSizes)
        {
            int indirectSize = 0;
            int renderInfoSize = 0;
            int indexSize = 0;
            int spaceVertexSize = 0;
            int paintVertexSize = 0;

            LogicalRegionChunk[] logicalChunks = logicalRegion._storedChunks;
            for (int i = 0; i < logicalChunks.Length; i++)
            {
                ref readonly LogicalRegionChunk chunk = ref logicalChunks[i];
                ref readonly ChunkMeshResult mesh = ref chunk.Mesh;

                if (mesh.IsEmpty)
                {
                    continue;
                }

                indirectSize += Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                renderInfoSize += Unsafe.SizeOf<ChunkRenderInfo>();
                indexSize += (int)mesh.IndexByteCount;
                spaceVertexSize += (int)mesh.SpaceVertexByteCount;
                paintVertexSize += (int)mesh.PaintVertexByteCount;
            }

            channelSizes = new ChannelSizes(5)
            {
                IndirectSize = (uint)indirectSize,
                RenderInfoSize = (uint)renderInfoSize,
                IndexSize = (uint)indexSize,
                SpaceVertexSize = (uint)spaceVertexSize,
                PaintVertexSize = (uint)paintVertexSize,
            };

            if (channelSizes.TotalSize > (uint)destination.Length)
            {
                return false;
            }

            Span<byte> indirectDst = destination[..indirectSize];
            destination = destination[indirectSize..];

            Span<byte> renderInfoDst = destination[..renderInfoSize];
            destination = destination[renderInfoSize..];

            Span<byte> indexDst = destination[..indexSize];
            destination = destination[indexSize..];

            Span<byte> spaceVertexDst = destination[..spaceVertexSize];
            destination = destination[spaceVertexSize..];

            Span<byte> paintVertexDst = destination[..paintVertexSize];
            destination = destination[paintVertexSize..];

            int indirectByteOffset = 0;
            int renderInfoByteOffset = 0;
            int indexByteOffset = 0;
            int spaceVertexByteOffset = 0;
            int paintVertexByteOffset = 0;

            uint indexOffset = 0;
            uint vertexOffset = 0;
            uint drawIndex = 0;

            for (int i = 0; i < logicalChunks.Length; i++)
            {
                ref readonly LogicalRegionChunk chunk = ref logicalChunks[i];
                ref readonly ChunkMeshResult mesh = ref chunk.Mesh;

                if (mesh.IsEmpty)
                {
                    continue;
                }

                uint indexCount = mesh.IndexCount;
                uint vertexCount = mesh.VertexCount;

                {
                    IndirectDrawIndexedArguments indirect = new()
                    {
                        FirstIndex = indexOffset,
                        FirstInstance = drawIndex,
                        InstanceCount = 1,
                        VertexOffset = (int)vertexOffset,
                        IndexCount = indexCount,
                    };
                    MemoryMarshal.Write(indirectDst[indirectByteOffset..], ref indirect);
                    indirectByteOffset += Unsafe.SizeOf<IndirectDrawIndexedArguments>();
                }

                {
                    ChunkRenderInfo renderInfo = new()
                    {
                        Translation = new Vector4(
                            chunk.Position.X * Chunk.Width,
                            chunk.Position.Y * Chunk.Height,
                            chunk.Position.Z * Chunk.Depth,
                            0)
                    };
                    MemoryMarshal.Write(renderInfoDst[renderInfoByteOffset..], ref renderInfo);
                    renderInfoByteOffset += Unsafe.SizeOf<ChunkRenderInfo>();
                }

                {
                    Span<byte> bytes = mesh.Indices.AsBytes();
                    bytes.CopyTo(indexDst[indexByteOffset..]);
                    indexByteOffset += bytes.Length;
                }

                {
                    Span<byte> bytes = mesh.SpaceVertices.AsBytes();
                    bytes.CopyTo(spaceVertexDst[spaceVertexByteOffset..]);
                    spaceVertexByteOffset += bytes.Length;
                }

                {
                    Span<byte> bytes = mesh.PaintVertices.AsBytes();
                    bytes.CopyTo(paintVertexDst[paintVertexByteOffset..]);
                    paintVertexByteOffset += bytes.Length;
                }

                indexOffset += indexCount;
                vertexOffset += vertexCount;
                drawIndex++;
            }

            uint bytesWritten =
                (uint)indirectByteOffset +
                (uint)renderInfoByteOffset +
                (uint)indexByteOffset +
                (uint)spaceVertexByteOffset +
                (uint)paintVertexByteOffset;

            Debug.Assert(bytesWritten == channelSizes.TotalSize);

            return true;
        }

        /*
        public bool Update()
        {
            int updateRequired = Interlocked.Exchange(ref _updateRequired, 0);
            if (updateRequired == 0)
            {
                return false;
            }

            VisualRegionChunk[] chunks = _storedChunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                ref VisualRegionChunk chunk = ref chunks[i];
                if (chunk.UpdateRequired != 0)
                {


                    chunk.RemoveRequired = 0;
                    chunk.UpdateRequired = 0;
                }
            }

            return true;
        }

        public void AddChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref VisualRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (!chunk.HasValue)
            {
                chunk.HasValue = true;
                chunk.RemoveRequired = 0;
            }
        }

        public void UpdateChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref VisualRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (chunk.HasValue)
            {
                chunk.UpdateRequired++;
                _updateRequired++;
            }
        }

        public void RemoveChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref VisualRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (chunk.HasValue)
            {
                chunk.HasValue = false;
                chunk.RemoveRequired++;
                chunk.UpdateRequired++;
                _updateRequired++;
            }
        }
        */

        public void SetPosition(RenderRegionPosition position)
        {
            Position = position;

            Size3 size = Size;
            ChunkPosition offsetPos = position.ToChunk(size);

            for (int y = 0; y < size.H; y++)
            {
                for (int z = 0; z < size.D; z++)
                {
                    for (int x = 0; x < size.W; x++)
                    {
                        ChunkPosition localPos = new(x, y, z);
                        ChunkPosition pos = offsetPos + localPos;
                        _storedChunks[GetStoredChunkIndex(GetLocalChunkPosition(pos))].Position = pos;
                    }
                }
            }
        }

        public ChunkPosition GetLocalChunkPosition(ChunkPosition chunkPosition)
        {
            return new ChunkPosition(
                (int)((uint)chunkPosition.X % Size.W),
                (int)((uint)chunkPosition.Y % Size.H),
                (int)((uint)chunkPosition.Z % Size.D));
        }

        private int GetStoredChunkIndex(ChunkPosition localPosition)
        {
            return (localPosition.Y * (int)Size.D + localPosition.Z) * (int)Size.W + localPosition.X;
        }

        private ref VisualRegionChunk GetStoredChunk(ChunkPosition localPosition)
        {
            int index = GetStoredChunkIndex(localPosition);
            return ref _storedChunks[index];
        }

        public void Dispose()
        {
            _isDisposed = true;

            SetMeshBuffers(null);
        }
    }

    public struct VisualRegionChunk
    {
        public bool HasValue;
        public ChunkPosition LocalPosition;
        public ChunkPosition Position;

        public int UpdateRequired;
        public int RemoveRequired;
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
    }
}
