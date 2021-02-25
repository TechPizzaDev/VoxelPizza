using System;

namespace VoxelPizza.Client
{
    public class Chunk
    {
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 16;

        public uint[,,] Blocks;

        public int ChunkX { get; }
        public int ChunkY { get; }
        public int ChunkZ { get; }

        public Chunk(int chunkX, int chunkY, int chunkZ)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            ChunkZ = chunkZ;

            Blocks = new uint[ChunkHeight, ChunkWidth, ChunkWidth];
        }

        public void Generate()
        {
            uint[,,] blocks = Blocks;

            var rng = new Random(HashCode.Combine(ChunkX, ChunkY, ChunkZ));

            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkWidth; z++)
                {
                    for (int x = 0; x < ChunkWidth; x++)
                    {
                        double fac = (ChunkY * 16 + y) / 2048.0;
                        if (rng.NextDouble() > 0.1 * fac)
                            continue;

                        blocks[y, z, x] = (uint)rng.Next(9);
                    }
                }
            }
        }
    }
}
