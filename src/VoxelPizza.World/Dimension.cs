using System;
using System.Collections.Generic;
using System.Threading;
using VoxelPizza.Diagnostics;
using VoxelPizza.Memory;

namespace VoxelPizza.World
{
    public delegate void ChunkAction(Chunk chunk);
    public delegate void ChunkRegionAction(ChunkRegion region);

    public partial class Dimension : IDestroyable
    {
        private Dictionary<ChunkRegionPosition, Arc<ChunkRegion>> _regions = new();
        private ReaderWriterLockSlim _regionLock = new();

        private HashSet<ChunkRegionPosition> _regionsToRemove = new();
        private Dictionary<ChunkRegionPosition, RegionStatistics> _regionStatistics = new();

        private ChunkAction _cachedChunkAdded;
        private ChunkAction _cachedChunkUpdated;
        private ChunkAction _cachedChunkRemoved;

        private ChunkRegionAction _cachedRegionEmpty;
        private ChunkRegionAction _cachedRegionDestroyed;

        public BlockPosition PlayerBlockPosition;

        public ChunkPosition PlayerChunkPosition => PlayerBlockPosition.ToChunk();

        /// <summary>
        /// Raised when a <see cref="Chunk"/> is added to this <see cref="Dimension"/>.
        /// </summary>
        /// <remarks>
        /// This event can be raised concurrently from different threads.
        /// </remarks>
        public event ChunkAction? ChunkAdded;

        /// <summary>
        /// Raised when a <see cref="Chunk"/> is updated within this <see cref="Dimension"/>.
        /// </summary>
        /// <remarks>
        /// This event can be raised concurrently from different threads.
        /// </remarks>
        public event ChunkAction? ChunkUpdated;

        /// <summary>
        /// Raised when a <see cref="Chunk"/> is removed from this <see cref="Dimension"/>.
        /// </summary>
        /// <remarks>
        /// This event can be raised concurrently from different threads.
        /// </remarks>
        public event ChunkAction? ChunkRemoved;

        /// <summary>
        /// Raised when a <see cref="ChunkRegion"/> is added to this <see cref="Dimension"/>.
        /// </summary>
        /// <remarks>
        /// This event can be raised concurrently from different threads.
        /// </remarks>
        public event ChunkRegionAction? RegionAdded;

        /// <summary>
        /// Raised when a <see cref="ChunkRegion"/> is removed from this <see cref="Dimension"/>.
        /// </summary>
        /// <remarks>
        /// This event can be raised concurrently from different threads.
        /// </remarks>
        public event ChunkRegionAction? RegionRemoved;

        public Dimension()
        {
            _cachedChunkAdded = Region_ChunkAdded;
            _cachedChunkUpdated = Region_ChunkUpdated;
            _cachedChunkRemoved = Region_ChunkRemoved;
            _cachedRegionEmpty = Region_Empty;
            _cachedRegionDestroyed = Region_Destroyed;
        }

        private void OnChunkRegionAdded(ChunkRegion chunkRegion)
        {
            RegionAdded?.Invoke(chunkRegion);

            _regionStatistics.Add(chunkRegion.Position, new RegionStatistics());
        }

        private void OnChunkRegionRemoved(ChunkRegion chunkRegion)
        {
            RegionRemoved?.Invoke(chunkRegion);

            _regionStatistics.Remove(chunkRegion.Position);
        }

        public void Update(Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            _regionsToRemove.Clear();

            _regionLock.EnterReadLock();
            try
            {
                foreach (Arc<ChunkRegion> regionArc in _regions.Values)
                {
                    ChunkRegion region = regionArc.Get();
                    if (_regionStatistics.TryGetValue(region.Position, out RegionStatistics? regionStats))
                    {
                        if (!region.HasChunks)
                        {
                            regionStats.UpdatesWithNoChunks++;

                            if (regionStats.UpdatesWithNoChunks > 100)
                            {
                                _regionsToRemove.Add(region.Position);
                            }
                        }
                        else
                        {
                            regionStats.UpdatesWithNoChunks = 0;
                        }
                    }
                }
            }
            finally
            {
                _regionLock.ExitReadLock();
            }

            foreach (ChunkRegionPosition regionPosition in _regionsToRemove)
            {
                RemoveRegion(regionPosition);
            }
        }

        private void Region_ChunkAdded(Chunk chunk)
        {
            ChunkAdded?.Invoke(chunk);
        }

        private void Region_ChunkUpdated(Chunk chunk)
        {
            ChunkUpdated?.Invoke(chunk);
        }

        private void Region_ChunkRemoved(Chunk chunk)
        {
            ChunkRemoved?.Invoke(chunk);
        }

        private void Region_Empty(ChunkRegion region)
        {
            RemoveRegion(region.Position);
        }

        private void Region_Destroyed(ChunkRegion region)
        {
            region.ChunkAdded -= _cachedChunkAdded;
            region.ChunkUpdated -= _cachedChunkUpdated;
            region.ChunkRemoved -= _cachedChunkRemoved;
            region.Empty -= _cachedRegionEmpty;
            region.Destroyed -= _cachedRegionDestroyed;

            region.Destroy();
        }

        public ValueArc<ChunkRegion> GetRegion(ChunkRegionPosition position)
        {
            // It is critical that no exception may be thrown inside the locked section.
            _regionLock.EnterReadLock();

            _regions.TryGetValue(position, out Arc<ChunkRegion>? region);
            ValueArc<ChunkRegion> result = region.TryTrack();

            _regionLock.ExitReadLock();
            return result;
        }

        public int GetRegions(ReadOnlySpan<ChunkRegionPosition> positions, Span<ValueArc<ChunkRegion>> regions)
        {
            regions = regions.Slice(0, positions.Length);
            
            // It is critical that no exception may be thrown inside the locked section.
            _regionLock.EnterReadLock();

            int count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                _regions.TryGetValue(positions[i], out Arc<ChunkRegion>? region);
                regions[i] = region.TryTrack();
                if (!regions[i].IsEmpty)
                {
                    count++;
                }
            }

            _regionLock.ExitReadLock();
            return count;
        }

        public static ValueArc<ChunkRegion> CreateRegion(ValueArc<Dimension> dimension, ChunkRegionPosition position)
        {
            Dimension self = dimension.Get();

            ValueArc<ChunkRegion> vRegion = self.GetRegion(position);
            if (vRegion.HasTarget)
            {
                // GetRegion increments refcount
                return vRegion;
            }

            return CreateRegionCore(dimension, position);
        }

        private static ValueArc<ChunkRegion> CreateRegionCore(ValueArc<Dimension> dimension, ChunkRegionPosition position)
        {
            Dimension self = dimension.Get();

            self._regionLock.EnterWriteLock();
            try
            {
                // Check again after acquiring lock,
                // as a region may have been created while we were waiting.
                if (!self._regions.TryGetValue(position, out Arc<ChunkRegion>? regionArc))
                {
                    ChunkRegion region = new(dimension, position);
                    region.ChunkAdded += self._cachedChunkAdded;
                    region.ChunkUpdated += self._cachedChunkUpdated;
                    region.ChunkRemoved += self._cachedChunkRemoved;
                    region.Empty += self._cachedRegionEmpty;
                    region.Destroyed += self._cachedRegionDestroyed;

                    regionArc = new Arc<ChunkRegion>(region);

                    self._regions.Add(region.Position, regionArc);
                    self.OnChunkRegionAdded(region);
                }

                return regionArc.Track();
            }
            finally
            {
                self._regionLock.ExitWriteLock();
            }
        }

        public ChunkRemoveStatus RemoveRegion(ChunkRegionPosition position)
        {
            _regionLock.EnterWriteLock();
            try
            {
                if (!_regions.Remove(position, out Arc<ChunkRegion>? region))
                {
                    return ChunkRemoveStatus.MissingRegion;
                }

                DecrementRegionRef(region);

                return ChunkRemoveStatus.Success;
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }
        }

        private void DecrementRegionRef(Arc<ChunkRegion> region)
        {
            // Invoke event before decrementing ref to let
            // others delay the unload.
            OnChunkRegionRemoved(region.Get());

            region.Decrement();
        }

        public ValueArc<Chunk> GetChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using ValueArc<ChunkRegion> regionArc = GetRegion(regionPosition);
            if (regionArc.TryGet(out ChunkRegion? region))
            {
                return region.GetChunk(position);
            }
            return ValueArc<Chunk>.Empty;
        }

        public static ValueArc<Chunk> CreateChunk(ValueArc<Dimension> dimension, ChunkPosition position, out ChunkAddStatus status)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using ValueArc<ChunkRegion> region = CreateRegion(dimension, regionPosition);
            return ChunkRegion.CreateChunk(region, position, out status);
        }

        public ChunkRemoveStatus RemoveChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using ValueArc<ChunkRegion> regionArc = GetRegion(regionPosition);
            if (regionArc.TryGet(out ChunkRegion? region))
            {
                return region.RemoveChunk(position);
            }
            return ChunkRemoveStatus.MissingRegion;
        }

        public void Destroy()
        {
        }

        private class RegionStatistics
        {
            public int UpdatesWithNoChunks;
        }
    }
}
