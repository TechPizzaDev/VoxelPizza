using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class RenderRegionGraph
    {
        public RenderRegionPosition RegionPosition { get; }

        public ChunkGraphFaces[] Chunks { get; }

        public event ChunkGraphSidesChanged? SidesFulfilled;
        public event ChunkGraphSidesChanged? SidesDisconnected;

        public RenderRegionGraph(RenderRegionPosition regionPosition, Size3 regionSize)
        {
            RegionPosition = regionPosition;
            Chunks = new ChunkGraphFaces[regionSize.Volume];
        }

        public ChunkGraphFaces Get(ChunkPosition localChunkPosition, Size3 regionSize)
        {
            int index = RenderRegionPosition.GetChunkIndex(localChunkPosition, regionSize);
            return Chunks[index];
        }

        public ChunkGraphFaces Add(ChunkPosition localChunkPosition, Size3 regionSize, ChunkGraphFaces value)
        {
            int index = RenderRegionPosition.GetChunkIndex(localChunkPosition, regionSize);
            ChunkGraphFaces oldValue = Chunks[index];
            ChunkGraphFaces newValue = oldValue | value;
            Chunks[index] = newValue;

            if ((oldValue & ChunkGraphFaces.All) != ChunkGraphFaces.All &&
                (newValue & ChunkGraphFaces.All) == ChunkGraphFaces.All)
            {
                SidesFulfilled?.Invoke(this, localChunkPosition, oldValue, newValue);
            }
            return newValue;
        }

        public ChunkGraphFaces Update(ChunkPosition localChunkPosition, Size3 regionSize, ChunkGraphFaces mask, ChunkGraphFaces value)
        {
            int index = RenderRegionPosition.GetChunkIndex(localChunkPosition, regionSize);
            ChunkGraphFaces oldValue = Chunks[index];
            ChunkGraphFaces newValue = (oldValue & ~mask) | (value & mask);
            Chunks[index] = newValue;
            return newValue;
        }

        public ChunkGraphFaces Remove(ChunkPosition localChunkPosition, Size3 regionSize, ChunkGraphFaces value)
        {
            int index = RenderRegionPosition.GetChunkIndex(localChunkPosition, regionSize);
            ChunkGraphFaces oldValue = Chunks[index];
            ChunkGraphFaces newValue = oldValue & (~value);
            Chunks[index] = newValue;

            if ((oldValue & ChunkGraphFaces.All) == ChunkGraphFaces.All &&
                (newValue & ChunkGraphFaces.All) != ChunkGraphFaces.All)
            {
                SidesDisconnected?.Invoke(this, localChunkPosition, oldValue, newValue);
            }
            return newValue;
        }
    }
}
