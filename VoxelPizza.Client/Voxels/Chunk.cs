using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class Chunk
    {
        public const int Width = 16;
        public const int Depth = 16;
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

        public void GetBlockRowUnsafe(nint y, nint z, ref uint destination)
        {
            ref uint blocksBase = ref MemoryMarshal.GetArrayDataReference(Blocks);
            ref ulong blocks = ref Unsafe.As<uint, ulong>(ref Unsafe.Add(ref blocksBase, Width * (y + Depth * z)));
            ref ulong dst = ref Unsafe.As<uint, ulong>(ref destination);

            Unsafe.Add(ref dst, 0) = Unsafe.Add(ref blocks, 0);
            Unsafe.Add(ref dst, 1) = Unsafe.Add(ref blocks, 1);
            Unsafe.Add(ref dst, 2) = Unsafe.Add(ref blocks, 2);
            Unsafe.Add(ref dst, 3) = Unsafe.Add(ref blocks, 3);
            Unsafe.Add(ref dst, 4) = Unsafe.Add(ref blocks, 4);
            Unsafe.Add(ref dst, 5) = Unsafe.Add(ref blocks, 5);
            Unsafe.Add(ref dst, 6) = Unsafe.Add(ref blocks, 6);
            Unsafe.Add(ref dst, 7) = Unsafe.Add(ref blocks, 7);
        }

        public void GetBlockSideRowUnsafe(nint y, nint x, ref uint destination)
        {
            ref uint blocks = ref MemoryMarshal.GetArrayDataReference(Blocks);

            Unsafe.Add(ref destination, 0) = Unsafe.Add(ref blocks, Width * (y + Depth * 0) + x);
            Unsafe.Add(ref destination, 1) = Unsafe.Add(ref blocks, Width * (y + Depth * 1) + x);
            Unsafe.Add(ref destination, 2) = Unsafe.Add(ref blocks, Width * (y + Depth * 2) + x);
            Unsafe.Add(ref destination, 3) = Unsafe.Add(ref blocks, Width * (y + Depth * 3) + x);
            Unsafe.Add(ref destination, 4) = Unsafe.Add(ref blocks, Width * (y + Depth * 4) + x);
            Unsafe.Add(ref destination, 5) = Unsafe.Add(ref blocks, Width * (y + Depth * 5) + x);
            Unsafe.Add(ref destination, 6) = Unsafe.Add(ref blocks, Width * (y + Depth * 6) + x);
            Unsafe.Add(ref destination, 7) = Unsafe.Add(ref blocks, Width * (y + Depth * 7) + x);
            Unsafe.Add(ref destination, 8) = Unsafe.Add(ref blocks, Width * (y + Depth * 8) + x);
            Unsafe.Add(ref destination, 9) = Unsafe.Add(ref blocks, Width * (y + Depth * 9) + x);
            Unsafe.Add(ref destination, 10) = Unsafe.Add(ref blocks, Width * (y + Depth * 10) + x);
            Unsafe.Add(ref destination, 11) = Unsafe.Add(ref blocks, Width * (y + Depth * 11) + x);
            Unsafe.Add(ref destination, 12) = Unsafe.Add(ref blocks, Width * (y + Depth * 12) + x);
            Unsafe.Add(ref destination, 13) = Unsafe.Add(ref blocks, Width * (y + Depth * 13) + x);
            Unsafe.Add(ref destination, 14) = Unsafe.Add(ref blocks, Width * (y + Depth * 14) + x);
            Unsafe.Add(ref destination, 15) = Unsafe.Add(ref blocks, Width * (y + Depth * 15) + x);
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
