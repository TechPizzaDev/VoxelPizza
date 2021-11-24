using System;
using System.Runtime.CompilerServices;
using VoxelPizza.Collections;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public class Chunk : RefCounted, IBlockStorage
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        public uint[] Blocks;
        public BlockStorage Storage;

        public event ChunkAction? Updated;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public WorldBox Box => new(Position.ToBlock(), Size);

        public ChunkRegion Region { get; }

        public Chunk(ChunkRegion region, ChunkPosition position)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Position = position;

            //Blocks = new uint[Height * Depth * Width];
            Storage = new BlockStorage(0);
        }

        public void InvokeUpdate()
        {
            Updated?.Invoke(this);
        }

        public void GetBlockRow(nint index, ref uint destination, uint length)
        {
            Storage.GetBlockRow(index, ref destination, length);
        }

        public void GetBlockRow(nint x, nint y, nint z, ref uint destination, uint length)
        {
            Storage.GetBlockRow(x, y, z, ref destination, length);
        }

        public void SetBlockLayer(nint y, uint value)
        {
            Storage.SetBlockLayer(y, value);
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

        public void SetBlock(nint x, nint y, nint z, uint value)
        {
            Storage.SetBlock(x, y, z, value);
        }

        public void SetBlock(nint index, uint value)
        {
            Storage.SetBlock(index, value);
        }

        public void Generate()
        {
            //uint[] blocks = Blocks;
            //ref uint blockRef = ref blocks[0];
            byte[] blocks8 = Storage._inlineStorage;
            ref byte blockRef8 = ref blocks8[0];
            ref ushort blockRef16 = ref Unsafe.As<byte, ushort>(ref blockRef8);

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
                    //double fac = (Y * Height + y) / 1024.0;
                    int blockY = chunkY + y;

                    for (int z = 0; z < Depth; z++)
                    {
                        int blockZ = chunkZ + z;
                        float cos = 64 * (MathF.Cos(blockZ / 16f) + 1) * 0.5f;

                        for (int x = 0; x < Width; x++)
                        {
                            //if (rng.NextDouble() > 0.025 * fac * 4)
                            //    continue;

                            int blockX = chunkX + x;

                            float sin = 64 * (MathF.Sin(blockX / 16f) + 1) * 0.5f;

                            nint i = GetIndex(x, y, z);

                            //ref uint block = ref Unsafe.Add(ref blockRef, i);
                            ref byte block8 = ref Unsafe.Add(ref blockRef8, i);
                            ref ushort block16 = ref Unsafe.Add(ref blockRef16, i);
                            
                            uint v = 0;
                            if ((sin + cos) * 0.5f >= blockY)
                                v = (uint)rng.Next(255) + 1;

                            //block = v;
                            block8 = v > 255 ? (byte)255 : (byte)v;
                            //block16 = v > ushort.MaxValue ? ushort.MaxValue : (ushort)v;
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
                            //Unsafe.Add(ref blockRef, GetIndex(x, z, z)) = (uint)(y + 1);
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
        public nint GetIndex(nint x, nint y, nint z)
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
