using System.Collections.Generic;
using System.Threading;

namespace VoxelPizza.World
{
    public delegate void ChunkAction(Chunk chunk);
    public delegate void ChunkRegionAction(ChunkRegion region);

    public class Dimension
    {
        private Dictionary<ChunkRegionPosition, ChunkRegion> _regions = new();
        private ReaderWriterLockSlim _regionLock = new();

        private HashSet<ChunkRegionPosition> _regionsToRemove = new();

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

        public void Update()
        {
            _regionsToRemove.Clear();

            _regionLock.EnterReadLock();
            try
            {
                foreach (ChunkRegion region in _regions.Values)
                {
                    if (!region.HasChunks)
                    {
                        _regionsToRemove.Add(region.Position);
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

        public ChunkRegion? GetRegion(ChunkRegionPosition position)
        {
            _regionLock.EnterReadLock();
            try
            {
                if (_regions.TryGetValue(position, out ChunkRegion? region))
                {
                    region.IncrementRef();
                    return region;
                }
                return null;
            }
            finally
            {
                _regionLock.ExitReadLock();
            }
        }

        public ChunkRegion CreateRegion(ChunkRegionPosition position)
        {
            ChunkRegion? region = GetRegion(position);
            if (region != null)
            {
                // GetRegion increments refcount
                return region;
            }

            _regionLock.EnterWriteLock();
            try
            {
                // Check again after acquiring lock,
                // as a region may have been created while we were waiting.
                if (!_regions.TryGetValue(position, out region))
                {
                    region = new ChunkRegion(this, position);
                    region.ChunkAdded += _cachedChunkAdded;
                    region.ChunkUpdated += _cachedChunkUpdated;
                    region.ChunkRemoved += _cachedChunkRemoved;
                    region.Empty += _cachedRegionEmpty;
                    region.RefZeroed += _cachedRegionRefZeroed;
                    region.IncrementRef(RefCountType.Container);

                    _regions.Add(region.Position, region);
                    RegionAdded?.Invoke(region);
                }
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }

            region.IncrementRef();
            return region;
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
                RegionRemoved?.Invoke(region);

                region.DecrementRef(RefCountType.Container);

                return ChunkRemoveStatus.Success;
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            ChunkRegion? region = GetRegion(regionPosition);
            if (region == null)
            {
                return null;
            }

            try
            {
                return region.GetChunk(position);
            }
            finally
            {
                region.DecrementRef();
            }
        }

        public ChunkAddStatus CreateChunk(ChunkPosition position, out Chunk? chunk)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            ChunkRegion region = CreateRegion(regionPosition);
            try
            {
                return region.CreateChunk(position, out chunk);
            }
            finally
            {
                region.DecrementRef();
            }
        }

        public ChunkRemoveStatus RemoveChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();
            ChunkRegion? region = GetRegion(regionPosition);
            if (region == null)
            {
                return ChunkRemoveStatus.MissingRegion;
            }

            try
            {
                return region.RemoveChunk(position);
            }
            finally
            {
                region.DecrementRef();
            }
        }
    }
}
