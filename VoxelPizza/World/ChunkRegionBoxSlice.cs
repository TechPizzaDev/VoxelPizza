using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public readonly struct ChunkRegionBoxSlice
    {
        public ChunkRegionPosition Region { get; }
        public ChunkPosition OuterOrigin { get; }
        public ChunkPosition InnerOrigin { get; }
        public Size3 Size { get; }

        public ChunkRegionBoxSlice(
            ChunkRegionPosition region,
            ChunkPosition outerOrigin,
            ChunkPosition innerOrigin,
            Size3 size)
        {
            Region = region;
            OuterOrigin = outerOrigin;
            InnerOrigin = innerOrigin;
            Size = size;
        }

        public ChunkBox GetChunkBox()
        {
            ChunkPosition baseOrigin = Region.ToChunk();
            ChunkPosition origin = baseOrigin + InnerOrigin;
            ChunkPosition max = origin + new ChunkPosition((int)Size.W, (int)Size.H, (int)Size.D);
            return new ChunkBox(origin, max);
        }
    }
}
