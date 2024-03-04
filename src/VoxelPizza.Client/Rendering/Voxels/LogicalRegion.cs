using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class LogicalRegion
    {
        public RenderRegionPosition Position { get; private set; }
        public Size3 Size { get; }

        public LogicalRegionChunk[] _storedChunks;
        private int _updateRequired;
        private int _chunkCount;

        public int ChunkCount => _chunkCount;

        public uint BytesForMesh;

        public LogicalRegion(Size3 size)
        {
            Size = size;

            _storedChunks = new LogicalRegionChunk[size.Volume];
            for (uint y = 0; y < size.H; y++)
            {
                for (uint z = 0; z < size.D; z++)
                {
                    for (uint x = 0; x < size.W; x++)
                    {
                        ChunkPosition localPos = new((int)x, (int)y, (int)z);
                        _storedChunks[RenderRegionPosition.GetChunkIndex(localPos, size)].LocalPosition = localPos;
                    }
                }
            }
        }

        public static uint GetBytesForMesh(in ChunkMeshResult mesh)
        {
            return mesh.IndexByteCount + mesh.SpaceVertexByteCount + mesh.PaintVertexByteCount;
        }

        public bool Update(ValueArc<Dimension> dimension, BlockMemory blockBuffer, ChunkMesher mesher)
        {
            int updateRequired = Interlocked.Exchange(ref _updateRequired, 0);
            if (updateRequired == 0)
            {
                return false;
            }

            Stopwatch fetchWatch = new();
            Stopwatch meshWatch = new();
            int meshCount = 0;
            int fetchCount = 0;

            BytesForMesh = 0;

            LogicalRegionChunk[] chunks = _storedChunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                ref LogicalRegionChunk chunk = ref chunks[i];
                if (!chunk.UpdateRequired)
                {
                    BytesForMesh += GetBytesForMesh(chunk.Mesh);
                    continue;
                }

                chunk.Version++;

                chunk.Mesh.Dispose();

                if (!chunk.IsEmpty)
                {
                    fetchWatch.Start();

                    BlockMemoryState memoryState = dimension.FetchBlockMemory(
                        blockBuffer, chunk.Position.ToBlock());

                    fetchWatch.Stop();
                    fetchCount++;

                    meshWatch.Start();
                    if (memoryState == BlockMemoryState.Filled)
                    {
                        chunk.Mesh = mesher.Mesh(blockBuffer);

                        BytesForMesh += GetBytesForMesh(chunk.Mesh);

                        meshCount++;
                    }
                    meshWatch.Stop();
                }

                chunk.UpdateRequired = false;
            }

            if (false)
            {
                string result = "";

                if (fetchCount > 0)
                {
                    result +=
                        $"Fetch {fetchCount} chunks: {fetchWatch.Elapsed.TotalMilliseconds:0.00}ms " +
                        $"({fetchWatch.Elapsed.TotalMilliseconds / fetchCount:0.000}ms avg)";
                }

                if (meshCount > 0)
                {
                    result +=
                        $"\nMesh {meshCount} chunks: {meshWatch.Elapsed.TotalMilliseconds:0.00}ms " +
                        $"({meshWatch.Elapsed.TotalMilliseconds / meshCount:0.000}ms avg)";
                }

                if (result != "")
                    Console.WriteLine(result);
            }

            return true;
        }

        public void AddChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, Size);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (chunk.HasValue)
            {
                return;
            }
            chunk.HasValue = true;

            _chunkCount++;
        }

        public void UpdateChunk(ChunkPosition chunkPosition, bool isEmpty)
        {
            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, Size);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (!chunk.HasValue)
            {
                return;
            }

            if (isEmpty && chunk.IsEmpty)
            {
                return;
            }

            chunk.IsEmpty = isEmpty;
            chunk.UpdateRequired = true;
            _updateRequired++;
        }

        public void RemoveChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, Size);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (!chunk.HasValue)
            {
                return;
            }
            chunk.HasValue = false;

            if (!chunk.IsEmpty)
            {
                chunk.IsEmpty = true;
                chunk.UpdateRequired = true;
                _updateRequired++;
            }

            _chunkCount--;
        }

        public void SetPosition(RenderRegionPosition position)
        {
            Position = position;

            Size3 size = Size;
            ChunkPosition offsetPos = position.ToChunk(size);

            LogicalRegionChunk[] chunks = _storedChunks;
            for (uint y = 0; y < size.H; y++)
            {
                for (uint z = 0; z < size.D; z++)
                {
                    for (uint x = 0; x < size.W; x++)
                    {
                        ChunkPosition localPos = new((int)x, (int)y, (int)z);
                        ChunkPosition pos = offsetPos + localPos;
                        Debug.Assert(localPos == RenderRegionPosition.GetLocalChunkPosition(pos, size));
                        chunks[RenderRegionPosition.GetChunkIndex(localPos, size)].Position = pos;
                    }
                }
            }
        }

        private ref LogicalRegionChunk GetStoredChunk(ChunkPosition localPosition)
        {
            int index = RenderRegionPosition.GetChunkIndex(localPosition, Size);
            return ref _storedChunks[index];
        }

        public void Dispose()
        {
            LogicalRegionChunk[] chunks = _storedChunks;
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i].Mesh.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct LogicalRegionChunk
    {
        public bool HasValue;
        public bool UpdateRequired;
        public bool IsEmpty;
        public ushort Version;

        public ChunkPosition LocalPosition;
        public ChunkPosition Position;

        public ChunkMeshResult Mesh;
    }
}
