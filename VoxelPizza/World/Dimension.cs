using System.Collections.Generic;
using System.Threading;

namespace VoxelPizza.World
{
    public delegate void ChunkAction(Chunk chunk);
    public delegate void RegionAction(ChunkRegion region);

    public class Dimension
    {
        private Dictionary<ChunkRegionPosition, ChunkRegion> _regions = new();
        private ReaderWriterLockSlim _regionLock = new();

        private ChunkAction _cachedChunkAdded;
        private ChunkAction _cachedChunkUpdated;
        private ChunkAction _cachedChunkRemoved;

        public ChunkPosition PlayerChunkPosition;

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkUpdated;
        public event ChunkAction? ChunkRemoved;

        public event RegionAction? RegionAdded;
        public event RegionAction? RegionRemoved;

        public Dimension()
        {
            _cachedChunkAdded = Region_ChunkAdded;
            _cachedChunkUpdated = Region_ChunkUpdated;
            _cachedChunkRemoved = Region_ChunkRemoved;
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
                if (_regions.TryGetValue(position, out region))
                {
                    region.IncrementRef();
                    return region;
                }

                region = new ChunkRegion(this, position);
                region.ChunkAdded += _cachedChunkAdded;
                region.ChunkUpdated += _cachedChunkUpdated;
                region.ChunkRemoved += _cachedChunkRemoved;
                region.IncrementRef(RefCountType.Container);

                _regions.Add(region.Position, region);
                RegionAdded?.Invoke(region);
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }

            region.IncrementRef();
            return region;
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();

            _regionLock.EnterReadLock();
            try
            {
                if (_regions.TryGetValue(regionPosition, out ChunkRegion? region))
                {
                    return region.GetChunk(position);
                }
                return null;
            }
            finally
            {
                _regionLock.ExitReadLock();
            }
        }

        public Chunk CreateChunk(ChunkPosition position)
        {
            ChunkRegionPosition regionPosition = position.ToRegion();

            ChunkRegion region = CreateRegion(regionPosition);
            try
            {
                return region.CreateChunk(position);
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
                return ChunkRemoveStatus.MissingRegion;

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
