using System;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkRegionGraph
    {
        public ChunkRegionPosition RegionPosition { get; }

        public ChunkGraphFaces[] Chunks { get; }

        public event Action<ChunkRegionGraph, ChunkPosition>? SidesFulfilled;

        public ChunkRegionGraph(ChunkRegionPosition regionPosition)
        {
            RegionPosition = regionPosition;
            Chunks = new ChunkGraphFaces[ChunkRegion.Size.Volume];
        }

        public ChunkGraphFaces Get(ChunkPosition localChunkPosition)
        {
            return Chunks[ChunkRegion.GetChunkIndex(localChunkPosition)];
        }

        public ChunkGraphFaces Add(ChunkPosition localChunkPosition, ChunkGraphFaces value)
        {
            ChunkGraphFaces newValue = Chunks[ChunkRegion.GetChunkIndex(localChunkPosition)] |= value;
            if ((newValue & ChunkGraphFaces.AllSides) == ChunkGraphFaces.AllSides &&
                (newValue & ChunkGraphFaces.Empty) == 0)
            {
                SidesFulfilled?.Invoke(this, localChunkPosition);
            }
            return newValue;
        }

        public ChunkGraphFaces Remove(ChunkPosition localChunkPosition, ChunkGraphFaces value)
        {
            return Chunks[ChunkRegion.GetChunkIndex(localChunkPosition)] &= ~value;
        }
    }
}
