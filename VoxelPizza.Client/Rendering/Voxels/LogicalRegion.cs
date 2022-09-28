using System;
using System.Diagnostics;
using System.Threading;
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

        public static uint GetBytesForMesh(in ChunkMeshResult mesh)
        {
            return mesh.IndexByteCount + mesh.SpaceVertexByteCount + mesh.PaintVertexByteCount;
        }

        public bool Update(Dimension dimension, BlockMemory blockBuffer, ChunkMesher mesher)
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

                if (!chunk.RemoveRequired)
                {
                    fetchWatch.Start();

                    BlockMemoryState memoryState = dimension.FetchBlockMemory(
                        blockBuffer, chunk.Position.ToBlock());

                    fetchWatch.Stop();
                    fetchCount++;

                    meshWatch.Start();
                    if (memoryState == BlockMemoryState.Filled)
                    {
                        chunk.Mesh.Dispose();
                        chunk.Mesh = mesher.Mesh(blockBuffer);

                        BytesForMesh += chunk.Mesh.IndexByteCount + chunk.Mesh.SpaceVertexByteCount + chunk.Mesh.PaintVertexByteCount;

                        meshCount++;
                    }
                    else
                    {
                        chunk.Mesh.Dispose();
                    }
                    meshWatch.Stop();
                }
                else
                {
                    chunk.Mesh.Dispose();
                }

                chunk.RemoveRequired = false;
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
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (!chunk.HasValue)
            {
                chunk.HasValue = true;
                chunk.RemoveRequired = false;
                _chunkCount++;
            }
        }

        public void UpdateChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (chunk.HasValue)
            {
                chunk.UpdateRequired = true;
                _updateRequired++;
            }
        }

        public void RemoveChunk(ChunkPosition chunkPosition)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(chunkPosition);
            ref LogicalRegionChunk chunk = ref GetStoredChunk(localPosition);
            if (chunk.HasValue)
            {
                chunk.HasValue = false;
                chunk.RemoveRequired = true;
                chunk.UpdateRequired = true;
                _updateRequired++;
                _chunkCount--;
            }
        }

        public void SetPosition(RenderRegionPosition position)
        {
            Position = position;

            Size3 size = Size;
            ChunkPosition offsetPos = position.ToChunk(size);

            LogicalRegionChunk[] chunks = _storedChunks;
            for (int y = 0; y < size.H; y++)
            {
                for (int z = 0; z < size.D; z++)
                {
                    for (int x = 0; x < size.W; x++)
                    {
                        ChunkPosition localPos = new(x, y, z);
                        ChunkPosition pos = offsetPos + localPos;
                        chunks[GetStoredChunkIndex(GetLocalChunkPosition(pos))].Position = pos;
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

        private ref LogicalRegionChunk GetStoredChunk(ChunkPosition localPosition)
        {
            int index = GetStoredChunkIndex(localPosition);
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

    public struct LogicalRegionChunk
    {
        public bool HasValue;
        public bool UpdateRequired;
        public bool RemoveRequired;
        public ushort Version;

        public ChunkPosition LocalPosition;
        public ChunkPosition Position;

        public ChunkMeshResult Mesh;
    }
}
