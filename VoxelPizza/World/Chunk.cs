using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public class Chunk
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        public uint[] Blocks;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public WorldBox Box => new(Position.ToBlock(), Size);

        public Chunk(ChunkPosition position)
        {
            Position = position;

            Blocks = new uint[Height * Depth * Width];
        }

        public void GetBlockRowUnsafe(nint x, nint y, nint z, ref uint destination, uint length)
        {
            ref uint blocksBase = ref MemoryMarshal.GetArrayDataReference(Blocks);
            ref byte blocks = ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref blocksBase, GetIndex(x, y, z)));
            ref byte dst = ref Unsafe.As<uint, byte>(ref destination);

            Unsafe.CopyBlockUnaligned(ref dst, ref blocks, length * sizeof(uint));
        }

        public void GetBlockSideRowUnsafe(nint y, nint x, ref uint destination)
        {
            ref uint blocks = ref MemoryMarshal.GetArrayDataReference(Blocks);

            Unsafe.Add(ref destination, 0) = Unsafe.Add(ref blocks, GetIndex(x, y, 0));
            Unsafe.Add(ref destination, 1) = Unsafe.Add(ref blocks, GetIndex(x, y, 1));
            Unsafe.Add(ref destination, 2) = Unsafe.Add(ref blocks, GetIndex(x, y, 2));
            Unsafe.Add(ref destination, 3) = Unsafe.Add(ref blocks, GetIndex(x, y, 3));
            Unsafe.Add(ref destination, 4) = Unsafe.Add(ref blocks, GetIndex(x, y, 4));
            Unsafe.Add(ref destination, 5) = Unsafe.Add(ref blocks, GetIndex(x, y, 5));
            Unsafe.Add(ref destination, 6) = Unsafe.Add(ref blocks, GetIndex(x, y, 6));
            Unsafe.Add(ref destination, 7) = Unsafe.Add(ref blocks, GetIndex(x, y, 7));
            Unsafe.Add(ref destination, 8) = Unsafe.Add(ref blocks, GetIndex(x, y, 8));
            Unsafe.Add(ref destination, 9) = Unsafe.Add(ref blocks, GetIndex(x, y, 9));
            Unsafe.Add(ref destination, 10) = Unsafe.Add(ref blocks, GetIndex(x, y, 10));
            Unsafe.Add(ref destination, 11) = Unsafe.Add(ref blocks, GetIndex(x, y, 11));
            Unsafe.Add(ref destination, 12) = Unsafe.Add(ref blocks, GetIndex(x, y, 12));
            Unsafe.Add(ref destination, 13) = Unsafe.Add(ref blocks, GetIndex(x, y, 13));
            Unsafe.Add(ref destination, 14) = Unsafe.Add(ref blocks, GetIndex(x, y, 14));
            Unsafe.Add(ref destination, 15) = Unsafe.Add(ref blocks, GetIndex(x, y, 15));
        }

        public void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            Blocks.AsSpan(GetIndex(x, y, z), destination.Length).CopyTo(destination);
        }

        public void GetBlockLayer(int y, Span<uint> destination)
        {
            Blocks.AsSpan(GetIndex(0, y, 0), Width * Depth).CopyTo(destination);
        }

        public void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            source.Slice(0, Width * Depth).CopyTo(Blocks.AsSpan(GetIndex(0, y, 0), Width * Depth));
        }

        public void SetBlockLayer(int y, uint source)
        {
            Blocks.AsSpan(GetIndex(0, y, 0), Width * Depth).Fill(source);
        }

        public uint GetBlock(int x, int y, int z)
        {
            return Blocks[GetIndex(x, y, z)];
        }

        public uint GetBlock(nint x, nint y, nint z)
        {
            return Blocks[GetIndex(x, y, z)];
        }

        public uint GetBlock(nint index)
        {
            return Blocks[index];
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
            int chunkZ = Z * Depth;

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

                            ref uint block = ref Unsafe.Add(ref blockRef, GetIndex(x, y, z));

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
                //blocks[GetIndex(1 + 2, 0 + 2, 1 + 2)] = (uint)1;
                //
                //blocks[GetIndex(2 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //blocks[GetIndex(1 + 2, 1 + 2, 0 + 2)] = (uint)1;
                //blocks[GetIndex(1 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //blocks[GetIndex(1 + 2, 1 + 2, 2 + 2)] = (uint)1;
                //blocks[GetIndex(0 + 2, 1 + 2, 1 + 2)] = (uint)1;
                //
                //blocks[GetIndex(1 + 2, 2 + 2, 1 + 2)] = (uint)1;

                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            Unsafe.Add(ref blockRef, GetIndex(x, z, z)) = (uint)(y + 1);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetIndex(nint x, nint y, nint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetIndexBase(uint depth, uint width, uint y, uint z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetIndexBase(nint depth, nint width, nint y, nint z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexBase(int depth, int width, int y, int z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkX(int blockX)
        {
            return blockX >> 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkY(int blockY)
        {
            return blockY >> 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkZ(int blockZ)
        {
            return blockZ >> 4;
        }
    }
}
