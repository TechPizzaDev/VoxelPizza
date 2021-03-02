using System;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class Chunk
    {
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 16;

        public uint[,,] Blocks;

        public ChunkPosition Position { get; }

        public int ChunkX => Position.X;
        public int ChunkY => Position.Y;
        public int ChunkZ => Position.Z;

        public Chunk(ChunkPosition position)
        {
            Position = position;

            Blocks = new uint[ChunkHeight, ChunkWidth, ChunkWidth];
        }

        public void Generate()
        {
            uint[,,] blocks = Blocks;

            int seed = 17;
            seed = seed * 31 + ChunkX;
            seed = seed * 31 + ChunkY;
            seed = seed * 31 + ChunkZ;
            var rng = new Random(seed);

            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkWidth; z++)
                {
                    for (int x = 0; x < ChunkWidth; x++)
                    {
                        double fac = (ChunkY * 16 + y) / 1024.0;
                        if (rng.NextDouble() > 0.025 * fac)
                            continue;

                        blocks[y, z, x] = (uint)rng.Next(9);
                    }
                }
            }
        }
    }
}
