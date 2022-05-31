using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public readonly struct ChunkBoxSlice
    {
        public readonly ChunkPosition Chunk;
        public readonly BlockPosition OuterOrigin;
        public readonly BlockPosition InnerOrigin;
        public readonly Size3 Size;

        public ChunkBoxSlice(ChunkPosition chunk, BlockPosition outerOrigin, BlockPosition innerOrigin, Size3 size)
        {
            Chunk = chunk;
            OuterOrigin = outerOrigin;
            InnerOrigin = innerOrigin;
            Size = size;
        }
    }
}
