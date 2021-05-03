using System;
using System.Runtime.CompilerServices;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class Chunk
    {
        public const int Width  = 16;
        public const int Depth  = 16;
        public const int Height = 16;

        public uint[] Blocks;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public Chunk(ChunkPosition position)
        {
            Position = position;

            Blocks = new uint[Height * Depth * Width];
        }

        public void GetBlockRow(int y, int z, Span<uint> destination)
        {
            Blocks.AsSpan(Width * (y + Depth * z), Width).CopyTo(destination);
        }

        public void GetBlockLayer(int y, Span<uint> destination)
        {
            Blocks.AsSpan(Width * (y + Depth), Width * Depth).CopyTo(destination);
        }

        public uint GetBlock(int x, int y, int z)
        {
            return Blocks[BlockPosition.GetIndex(x, y, z)];
        }

        public void Generate()
        {
            uint[] blocks = Blocks;
            ref uint blockRef = ref blocks[0];

            int seed = 17;
            seed = seed * 31 + X;
            seed = seed * 31 + Y;
            seed = seed * 31 + Z;
            var rng = new Random(seed);

            int chunkX = X * Width;
            int chunkY = Y * Height;
            int chunkZ = Z * Width;

            if (true)
            {
                for (int y = 0; y < Height; y++)
                {
                    double fac = (Y * Height + y) / 1024.0;

                    for (int z = 0; z < Depth; z++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            //if (rng.NextDouble() > 0.025 * fac * 4)
                            //    continue;

                            int blockX = chunkX + x;
                            int blockY = chunkY + y;
                            int blockZ = chunkZ + z;

                            float sin = 64 * (MathF.Sin(blockX / 16f) + 1) * 0.5f;
                            float cos = 64 * (MathF.Cos(blockZ / 16f) + 1) * 0.5f; 

                            ref uint block = ref Unsafe.Add(ref blockRef, BlockPosition.GetIndex(x, y, z));

                            if ((sin + cos) * 0.5f >= blockY)
                                block = (uint)rng.Next(1024) + 1;
                            else
                                block = 0;
                        }
                    }
                }
            }
            else
            {
                //blocks[BlockPosition.GetIndex(1 + 2, 0 + 2, 1 + 2)] = (uint)1;
                //
                //blocks[BlockPosition.GetIndex(2 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //blocks[BlockPosition.GetIndex(1 + 2, 1 + 2, 0 + 2)] = (uint)1;
                //blocks[BlockPosition.GetIndex(1 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //blocks[BlockPosition.GetIndex(1 + 2, 1 + 2, 2 + 2)] = (uint)1;
                //blocks[BlockPosition.GetIndex(0 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //
                //blocks[BlockPosition.GetIndex(1 + 2, 2 + 2, 1 + 2)] = (uint)1;

                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            Unsafe.Add(ref blockRef, BlockPosition.GetIndex(x, z, z)) = (uint)(y + 1);
                        }
                    }
                }
            }
        }
    }
}
