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
        private ChunkAction _cachedChunkRemoved;

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkRemoved;

        public event RegionAction? RegionAdded;
        public event RegionAction? RegionRemoved;

        public Dimension()
        {
            _cachedChunkAdded = Region_ChunkAdded;
            _cachedChunkRemoved = Region_ChunkRemoved;
        }

        private void Region_ChunkAdded(Chunk chunk)
        {
            ChunkAdded?.Invoke(chunk);
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
                return region;
            }

            _regionLock.EnterWriteLock();
            try
            {
                region = new ChunkRegion(this, position);
                region.ChunkAdded += _cachedChunkAdded;
                region.ChunkRemoved += _cachedChunkRemoved;

                _regions.Add(region.Position, region);
            }
            finally
            {
                _regionLock.ExitWriteLock();
            }

            region.IncrementRef();
            RegionAdded?.Invoke(region);
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
    }
}
