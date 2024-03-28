using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using VoxelPizza.Diagnostics;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class RenderRegionManager : IUpdateable
    {
        /// <summary>
        /// The amount of blocks that are fetched around a chunk for meshing.
        /// </summary>
        private const uint FetchMargin = 2;

        private ConcurrentQueue<ChunkChange> _chunkChanges = new();
        private Dictionary<RenderRegionPosition, LogicalRegion> _regions = new();
        private LogicalRegion[] _regionArray = Array.Empty<LogicalRegion>();
        private int[] _regionDistanceArray = Array.Empty<int>();
        private ValueArc<Dimension> _dimension;

        public ValueArc<Dimension> Dimension => _dimension.Wrap();
        public MemoryHeap ChunkMeshHeap { get; }
        public Size3 RegionSize { get; }

        public ChunkMesher ChunkMesher { get; }

        private BlockMemory _blockBuffer;

        private HashSet<ChunkUpdateEvent> _chunksToAdd = new();
        private HashSet<ChunkUpdateEvent> _chunksToUpdate = new();
        private HashSet<ChunkPosition> _chunksToRemove = new();
        private bool _pendingRegionUpdate;
        private Stopwatch _updateWatch = new();

        private ChunkGraph _graph;

        public event Action<LogicalRegion>? RegionAdded;
        public event Action<LogicalRegion>? RegionUpdated;
        public event Action<LogicalRegion>? RegionRemoved;

        public RenderRegionManager(ValueArc<Dimension> dimension, MemoryHeap chunkMeshHeap, Size3 regionSize)
        {
            _dimension = dimension.Track();
            ChunkMeshHeap = chunkMeshHeap ?? throw new ArgumentNullException(nameof(chunkMeshHeap));
            RegionSize = regionSize;

            ChunkMesher = new ChunkMesher(ChunkMeshHeap);

            Dimension dim = dimension.Get();
            dim.ChunkAdded += Dimension_ChunkAdded;
            dim.ChunkUpdated += Dimension_ChunkUpdated;
            dim.ChunkRemoved += Dimension_ChunkRemoved;

            _blockBuffer = new BlockMemory(
                GetBlockMemoryInnerSize(),
                GetBlockMemoryOuterSize(FetchMargin));

            _graph = new ChunkGraph(RegionSize);
            _graph.SidesFulfilled += Graph_SidesFulfilled;
        }

        public void IterateMeshes(Predicate<LogicalRegion> transform)
        {
            lock (_regions)
            {
                foreach (LogicalRegion item in _regions.Values)
                {
                    if (!transform.Invoke(item))
                    {
                        break;
                    }
                }
            }
        }

        private void Graph_SidesFulfilled(
            RenderRegionGraph graph,
            ChunkPosition localPosition,
            ChunkGraphFaces oldFlags,
            ChunkGraphFaces newFlags)
        {
            if ((newFlags & ChunkGraphFaces.Update) == 0)
            {
                return;
            }

            ChunkPosition position = graph.RegionPosition.ToChunk(_graph.RegionSize) + localPosition;

            // Do not pass along Update flag.
            ChunkUpdateEvent ev = new(position, newFlags & ChunkGraphFaces.Empty);
            _chunksToUpdate.Add(ev);
        }

        public (uint Sum, uint Avg) GetBytesForMeshes()
        {
            uint sum = 0;
            uint count = 0;

            lock (_regions)
            {
                if (_regions.Count == 0)
                {
                    return default;
                }

                foreach (LogicalRegion item in _regions.Values)
                {
                    sum += item.BytesForMesh;
                    count++;
                }
            }

            return (sum, sum / count);
        }

        private void ProcessChunkChanges(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            _updateWatch.Restart();

            long chunkChangeCount = 0;
            while (_chunkChanges.TryDequeue(out ChunkChange change))
            {
                switch (change.Type)
                {
                    case ChunkChangeType.Add:
                        _chunksToAdd.Add(change.Data);
                        _chunksToRemove.Remove(change.Data.Position);
                        break;

                    case ChunkChangeType.Update:
                        _chunksToUpdate.Add(change.Data);
                        break;

                    case ChunkChangeType.Remove:
                        _chunksToAdd.Remove(change.Data);
                        _chunksToRemove.Add(change.Data.Position);
                        break;
                }

                chunkChangeCount++;
            }

            _updateWatch.Stop();

            if (chunkChangeCount > 0)
            {
                Console.WriteLine($"Processed {chunkChangeCount} chunk changes in {_updateWatch.Elapsed.TotalMilliseconds:0.00}ms");
            }
        }

        private void ProcessChunksToRemove(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            if (_chunksToRemove.Count <= 0)
            {
                return;
            }

            foreach (ChunkPosition chunkPosition in _chunksToRemove)
            {
                // We processed as many events as possible.
                // If a chunk is now marked for removal, stop its update request.
                if (_chunksToUpdate.Count > 0)
                {
                    _chunksToUpdate.Remove(new ChunkUpdateEvent(chunkPosition, ChunkGraphFaces.None));
                }

                RenderRegionPosition regionPosition = new(chunkPosition, RegionSize);
                if (_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                {
                    region.RemoveChunk(chunkPosition);
                    _graph.RemoveChunk(chunkPosition);

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

        private void ProcessChunksToAdd(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            if (_chunksToAdd.Count <= 0)
            {
                return;
            }

            foreach (ChunkUpdateEvent update in _chunksToAdd)
            {
                RenderRegionPosition regionPosition = new(update.Position, RegionSize);
                if (!_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                {
                    region = new LogicalRegion(RegionSize);
                    region.SetPosition(regionPosition);
                    _regions.Add(regionPosition, region);

                    RegionAdded?.Invoke(region);
                }

                region.AddChunk(update.Position);
                _graph.AddChunk(update.Position, update.Flags);
            }

            _pendingRegionUpdate = true;
            _chunksToAdd.Clear();
        }

        private void ProcessChunksToUpdate(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            if (_chunksToUpdate.Count <= 0)
            {
                return;
            }

            foreach (ChunkUpdateEvent update in _chunksToUpdate)
            {
                ChunkGraphFaces newFlags = _graph.UpdateChunk(update.Position, update.Flags);
                if ((newFlags & ChunkGraphFaces.All) == ChunkGraphFaces.All)
                {
                    RenderRegionPosition regionPosition = new(update.Position, RegionSize);
                    if (_regions.TryGetValue(regionPosition, out LogicalRegion? region))
                    {
                        region.UpdateChunk(update.Position, newFlags.HasFlag(ChunkGraphFaces.Empty));
                    }
                }
            }

            _pendingRegionUpdate = true;
            _chunksToUpdate.Clear();
        }

        private void UpdateRegions(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            if (!_pendingRegionUpdate)
            {
                return;
            }

            CancellationTokenSource cancellationSource = new(TimeSpan.FromMilliseconds(10));
            bool wasBroken = false;

            _updateWatch.Restart();

            Dimension dim = Dimension.Get();
            ChunkPosition origin = dim.PlayerChunkPosition;

            Span<LogicalRegion> sortedRegions = GetSortedRegions(origin);

            foreach (LogicalRegion region in sortedRegions)
            {
                bool updated = region.Update(Dimension, _blockBuffer, ChunkMesher, cancellationSource.Token);
                if (updated)
                {
                    RegionUpdated?.Invoke(region);
                }

                if (cancellationSource.IsCancellationRequested)
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

        public void Update(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            ProcessChunkChanges(state.Profiler);

            ProcessChunksToRemove(state.Profiler);

            ProcessChunksToAdd(state.Profiler);

            ProcessChunksToUpdate(state.Profiler);

            UpdateRegions(state.Profiler);
        }

        private Span<LogicalRegion> GetSortedRegions(ChunkPosition origin)
        {
            int count = _regions.Values.Count;
            if (_regionArray.Length < count)
            {
                int newSize = (count + 63) / 64 * 64;
                Array.Resize(ref _regionArray, newSize);
                Array.Resize(ref _regionDistanceArray, newSize);
            }

            _regions.Values.CopyTo(_regionArray, 0);

            Span<LogicalRegion> regions = _regionArray.AsSpan(0, count);
            Span<int> regionDistances = _regionDistanceArray.AsSpan(0, count);

            ChunkPosition offsetOrigin = origin - new ChunkPosition(
                (int)(RegionSize.W / 2),
                (int)(RegionSize.H / 2),
                (int)(RegionSize.D / 2));

            for (int i = 0; i < regions.Length; i++)
            {
                regionDistances[i] = GetDistance(regions[i].Position.ToChunk(RegionSize), offsetOrigin);
            }
            regionDistances.Sort(regions);

            return regions;
        }

        private static int GetDistance(ChunkPosition left, ChunkPosition right)
        {
            int dx = IntMath.Abs(right.X - left.X);
            int dy = IntMath.Abs(right.Y - left.Y);
            int dz = IntMath.Abs(right.Z - left.Z);
            return dx + dy + dz;
        }

        private void Dimension_ChunkAdded(Chunk chunk)
        {
            ChunkGraphFaces flags = ChunkUpdateEvent.GetFlags(chunk);
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Add, new ChunkUpdateEvent(chunk.Position, flags)));
        }

        private void Dimension_ChunkUpdated(Chunk chunk)
        {
            ChunkGraphFaces flags = ChunkUpdateEvent.GetFlags(chunk);
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Update, new ChunkUpdateEvent(chunk.Position, flags)));
        }

        private void Dimension_ChunkRemoved(Chunk chunk)
        {
            _chunkChanges.Enqueue(new ChunkChange(ChunkChangeType.Remove, new ChunkUpdateEvent(chunk.Position, ChunkGraphFaces.None)));
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

        private enum ChunkChangeType : byte
        {
            Add,
            Update,
            Remove
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ChunkChange(ChunkChangeType type, ChunkUpdateEvent data)
        {
            public ChunkChangeType Type { get; } = type;
            public ChunkUpdateEvent Data { get; } = data;
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ChunkUpdateEvent(ChunkPosition position, ChunkGraphFaces flags) : IEquatable<ChunkUpdateEvent>
        {
            public ChunkPosition Position { get; } = position;
            public ChunkGraphFaces Flags { get; } = flags;

            public bool Equals(ChunkUpdateEvent other)
            {
                return Position == other.Position;
            }

            public override int GetHashCode()
            {
                return Position.GetHashCode();
            }

            public static ChunkGraphFaces GetFlags(Chunk chunk)
            {
                ChunkGraphFaces flags = ChunkGraphFaces.None;
                if (chunk.IsEmpty)
                {
                    flags |= ChunkGraphFaces.Empty;
                }
                return flags;
            }
        }
    }
}
