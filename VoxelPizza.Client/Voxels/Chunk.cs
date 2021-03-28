using System;
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

            int seed = 17;
            seed = seed * 31 + X;
            seed = seed * 31 + Y;
            seed = seed * 31 + Z;
            var rng = new Random(seed);

            if (false)
            {
                for (int y = 0; y < Height; y++)
                {
                    double fac = (Y * Chunk.Height + y) / 1024.0;

                    for (int z = 0; z < Depth; z++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            if (rng.NextDouble() > 0.025 * fac)
                                continue;

                            blocks[BlockPosition.GetIndex(x, z, z)] = (uint)rng.Next(9);
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
                            blocks[BlockPosition.GetIndex(x, y, z)] = (uint)(y + 1);
                        }
                    }
                }
            }
        }
    }
}
