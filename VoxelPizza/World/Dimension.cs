using System.Collections.Generic;
using System.Threading;
using VoxelPizza.Diagnostics;
using VoxelPizza.Memory;

namespace VoxelPizza.World
{
    public delegate void ChunkAction(Chunk chunk);
    public delegate void ChunkRegionAction(ChunkRegion region);

    public partial class Dimension : RefCounted
    {
        private Dictionary<ChunkRegionPosition, ChunkRegion> _regions = new();
        private ReaderWriterLockSlim _regionLock = new();

        private HashSet<ChunkRegionPosition> _regionsToRemove = new();
        private Dictionary<ChunkRegionPosition, RegionStatistics> _regionStatistics = new();

        private ChunkAction _cachedChunkAdded;
        private ChunkAction _cachedChunkUpdated;
        private ChunkAction _cachedChunkRemoved;
        private ChunkRegionAction _cachedRegionEmpty;
        private RefCountedAction _cachedRegionRefZeroed;

        public ChunkPosition PlayerChunkPosition;

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
            _cachedRegionRefZeroed = Region_RefZeroed;
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
                foreach (ChunkRegion region in _regions.Values)
                {
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

        private void Region_RefZeroed(RefCounted instance)
        {
            ChunkRegion region = (ChunkRegion)instance;

            region.ChunkAdded -= _cachedChunkAdded;
            region.ChunkUpdated -= _cachedChunkUpdated;
            region.ChunkRemoved -= _cachedChunkRemoved;
            region.Empty -= _cachedRegionEmpty;
            region.RefZeroed -= _cachedRegionRefZeroed;

            region.Destroy();
        }

        public RefCounted<ChunkRegion?> GetRegion(ChunkRegionPosition position)
        {
            _regionLock.EnterReadLock();
            try
            {
                if (_regions.TryGetValue(position, out ChunkRegion? region))
                {
                    return region.TrackRef()!;
                }
                return default;
            }
            finally
            {
                _regionLock.ExitReadLock();
            }
        }

        public RefCounted<ChunkRegion> CreateRegion(ChunkRegionPosition position)
        {
            RefCounted<ChunkRegion?> countedRegion = GetRegion(position);
            if (countedRegion.HasValue)
            {
                // GetRegion increments refcount
                return countedRegion!;
            }

            _regionLock.EnterWriteLock();
            try
            {
                // Check again after acquiring lock,
                // as a region may have been created while we were waiting.
                if (!_regions.TryGetValue(position, out ChunkRegion? region))
                {
                    region = new ChunkRegion(this, position);
                    region.ChunkAdded += _cachedChunkAdded;
                    region.ChunkUpdated += _cachedChunkUpdated;
                    region.ChunkRemoved += _cachedChunkRemoved;
                    region.Empty += _cachedRegionEmpty;
                    region.RefZeroed += _cachedRegionRefZeroed;
                    region.IncrementRef(RefCountType.Container);

                    _regions.Add(region.Position, region);
                    OnChunkRegionAdded(region);
                }

                return region.TrackRef();
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }
        }

        public ChunkRemoveStatus RemoveRegion(ChunkRegionPosition position)
        {
            _regionLock.EnterWriteLock();
            try
            {
                if (!_regions.Remove(position, out ChunkRegion? region))
                {
                    return ChunkRemoveStatus.MissingRegion;
                }

                // Invoke event before decrementing ref to let
                // others delay the unload.
                OnChunkRegionRemoved(region);

                region.DecrementRef(RefCountType.Container);

                return ChunkRemoveStatus.Success;
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }
        }

        public RefCounted<Chunk?> GetChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using RefCounted<ChunkRegion?> countedRegion = GetRegion(regionPosition);
            if (countedRegion.TryGetValue(out ChunkRegion? region))
            {
                return region.GetChunk(position);
            }
            return default;
        }

        public RefCounted<Chunk> CreateChunk(ChunkPosition position, out ChunkAddStatus status)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using RefCounted<ChunkRegion> region = CreateRegion(regionPosition);
            return region.Value.CreateChunk(position, out status);
        }

        public ChunkRemoveStatus RemoveChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            using RefCounted<ChunkRegion?> countedRegion = GetRegion(regionPosition);
            if (countedRegion.TryGetValue(out ChunkRegion? region))
            {
                return region.RemoveChunk(position);
            }
            return ChunkRemoveStatus.MissingRegion;
        }

        private class RegionStatistics
        {
            public int UpdatesWithNoChunks;
        }
    }
}
