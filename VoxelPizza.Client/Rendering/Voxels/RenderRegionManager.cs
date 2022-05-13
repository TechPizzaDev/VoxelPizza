using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class RenderRegionManager : IUpdateable
    {
        private const uint FetchMargin = 2;

        private ConcurrentQueue<ChunkChange> _chunkChanges = new();
        private Dictionary<RenderRegionPosition, LogicalRegion> _regions = new();

        public Dimension Dimension { get; }
        public MemoryHeap ChunkMeshHeap { get; }
        public Size3 RegionSize { get; }

        public ChunkMesher ChunkMesher { get; }

        private BlockMemory _blockBuffer;

        private HashSet<ChunkPosition> _chunksToAdd = new();
        private HashSet<ChunkPosition> _chunksToUpdate = new();
        private HashSet<ChunkPosition> _chunksToRemove = new();
        private bool _pendingRegionUpdate;
        private Stopwatch _updateWatch = new();

        public event Action<LogicalRegion>? RegionAdded;
        public event Action<LogicalRegion>? RegionUpdated;
        public event Action<LogicalRegion>? RegionRemoved;

        public RenderRegionManager(Dimension dimension, MemoryHeap chunkMeshHeap, Size3 regionSize)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            ChunkMeshHeap = chunkMeshHeap ?? throw new ArgumentNullException(nameof(chunkMeshHeap));
            RegionSize = regionSize;

            ChunkMesher = new ChunkMesher(ChunkMeshHeap);

            dimension.ChunkAdded += Dimension_ChunkAdded;
            dimension.ChunkUpdated += Dimension_ChunkUpdated;
            dimension.ChunkRemoved += Dimension_ChunkRemoved;

            _blockBuffer = new BlockMemory(
                GetBlockMemoryInnerSize(),
                GetBlockMemoryOuterSize(FetchMargin));
        }

        public void Update(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            while (_chunkChanges.TryDequeue(out ChunkChange change))
            {
                switch (change.Type)
                {
                    case ChunkChangeType.Add:
                        _chunksToAdd.Add(change.Chunk);
                        _chunksToRemove.Remove(change.Chunk);
                        break;

                    case ChunkChangeType.Update:
                        _chunksToUpdate.Add(change.Chunk);
                        break;

                    case ChunkChangeType.Remove:
                        _chunksToAdd.Remove(change.Chunk);
                        _chunksToRemove.Add(change.Chunk);
                        break;
                }
            }

            if (_chunksToRemove.Count > 0)
            {
                foreach (ChunkPosition chunkPosition in _chunksToRemove)
                {
                    // We processed as many events as possible.
                    // If a chunk is now marked for removal, stop its update request.
                    if (_chunksToUpdate.Count > 0)
                    {
                        _chunksToUpdate.Remove(chunkPosition);
                    }

                    RenderRegionPosition regionPosition = new(chunkPosition, RegionSize);
                    if (_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                    {
                        region.RemoveChunk(chunkPosition);

                        if (region.ChunkCount == 0)
                        {
                            RegionRemoved?.Invoke(region);

                            _regions.Remove(regionPosition);
                            region.Dispose();
                        }
                    }
                }

                _pendingRegionUpdate = true;
                _chunksToRemove.Clear();
            }

            if (_chunksToAdd.Count > 0)
            {
                foreach (ChunkPosition chunkPosition in _chunksToAdd)
                {
                    RenderRegionPosition regionPosition = new(chunkPosition, RegionSize);
                    if (!_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                    {
                        region = new LogicalRegion(RegionSize);
                        region.SetPosition(regionPosition);
                        _regions.Add(regionPosition, region);

                        RegionAdded?.Invoke(region);
                    }
                    region.AddChunk(chunkPosition);
                }

                _pendingRegionUpdate = true;
                _chunksToAdd.Clear();
            }

            if (_chunksToUpdate.Count > 0)
            {
                foreach (ChunkPosition chunkPosition in _chunksToUpdate)
                {
                    RenderRegionPosition regionPosition = new(chunkPosition, RegionSize);
                    if (_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                    {
                        region.UpdateChunk(chunkPosition);
                    }
                }

                _pendingRegionUpdate = true;
                _chunksToUpdate.Clear();
            }

            if (_pendingRegionUpdate)
            {
                bool wasBroken = false;

                _updateWatch.Restart();
                foreach (LogicalRegion region in _regions.Values)
                {
                    bool updated = region.Update(Dimension, _blockBuffer, ChunkMesher);
                    if (updated)
                    {
                        RegionUpdated?.Invoke(region);
                    }

                    if (_updateWatch.Elapsed >= TimeSpan.FromMilliseconds(10))
                    {
                        wasBroken = true;
                        break;
                    }
                }
                _updateWatch.Stop();

                if (!wasBroken)
                {
                    _pendingRegionUpdate = false;
                }
            }
        }

        private void Dimension_ChunkAdded(Chunk chunk)
        {
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Add, chunk.Position));
        }

        private void Dimension_ChunkUpdated(Chunk chunk)
        {
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Update, chunk.Position));
        }

        private void Dimension_ChunkRemoved(Chunk chunk)
        {
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Remove, chunk.Position));
        }

        public bool TryGetLogicalRegion(RenderRegionPosition regionPosition, [MaybeNullWhen(false)] out LogicalRegion region)
        {
            return _regions.TryGetValue(regionPosition, out region);
        }

        public Size3 GetBlockMemoryInnerSize()
        {
            return Chunk.Size;
        }

        public Size3 GetBlockMemoryOuterSize(uint margin)
        {
            uint doubleMargin = margin * 2;

            Size3 innerSize = GetBlockMemoryInnerSize();

            return new Size3(
                innerSize.W + doubleMargin,
                innerSize.H + doubleMargin,
                innerSize.D + doubleMargin);
        }

        private enum ChunkChangeType
        {
            Add,
            Update,
            Remove
        }

        private record struct ChunkChange(ChunkChangeType Type, ChunkPosition Chunk);
    }
}
