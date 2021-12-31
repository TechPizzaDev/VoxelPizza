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

        private BlockStorage? _storage;

        public event ChunkAction? Updated;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public ChunkRegion Region { get; }

        public BlockStorageType StorageType => _storage?.StorageType ?? BlockStorageType.Null;
        ushort IBlockStorage.Width => _storage?.Width ?? 0;
        ushort IBlockStorage.Height => _storage?.Height ?? 0;
        ushort IBlockStorage.Depth => _storage?.Depth ?? 0;
        public bool IsEmpty => _storage == null || _storage.IsEmpty;

        public Chunk(ChunkRegion region, ChunkPosition position)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Position = position;
        }

        public void InvokeUpdate()
        {
            Updated?.Invoke(this);
        }

        public BlockStorage GetBlockStorage()
        {
            if (_storage == null)
            {
                _storage = new BlockStorage8(Width, Height, Depth);
            }
            return _storage;
        }

        public void GetBlockRow(nuint index, ref uint destination, nuint length)
        {
            GetBlockStorage().GetBlockRow(index, ref destination, length);
        }

        public void GetBlockRow(nuint x, nuint y, nuint z, ref uint destination, nuint length)
        {
            GetBlockStorage().GetBlockRow(x, y, z, ref destination, length);
        }

        public void SetBlockLayer(nuint y, uint value)
        {
            GetBlockStorage().SetBlockLayer(y, value);
        }

        public uint GetBlock(int x, int y, int z)
        {
            throw new NotImplementedException();
            //return Blocks[GetIndex(x, y, z)];
        }

        public uint GetBlock(nint x, nint y, nint z)
        {
            throw new NotImplementedException();
            //return Blocks[GetIndex(x, y, z)];
        }

        public uint GetBlock(nint index)
        {
            throw new NotImplementedException();
            //return Blocks[index];
        }

        public void SetBlock(nuint x, nuint y, nuint z, uint value)
        {
            GetBlockStorage().SetBlock(x, y, z, value);
        }

        public void SetBlock(nuint index, uint value)
        {
            GetBlockStorage().SetBlock(index, value);
        }

        public void Generate()
        {
            //uint[] blocks = Blocks;
            //ref uint blockRef = ref blocks[0];

            if (!TryGetInline(out Span<byte> blocks8, out BlockStorageType storageType))
            {
                throw new Exception();
            }

            ref byte blockRef8 = ref blocks8[0];
            ref ushort blockRef16 = ref Unsafe.As<byte, ushort>(ref blockRef8);

            ulong seed = 17;
            seed = seed * 31 + (uint)X;
            seed = seed * 31 + (uint)Y;
            seed = seed * 31 + (uint)Z;
            var rng = new XoshiroRandom(seed);

            int chunkX = X * Width;
            int chunkY = Y * Height;
            int chunkZ = Z * Depth;

            if (true)
            {
                Span<byte> tmp = stackalloc byte[Width * Depth];

                for (int y = 0; y < Height; y++)
                {
                    //double fac = (Y * Height + y) / 1024.0;
                    int blockY = chunkY + y;

                    rng.NextBytes(tmp);

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
                                v = (uint)tmp[x + z * Width] + 1;

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
        public nuint GetIndex(nuint x, nuint y, nuint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetIndexBase(uint depth, uint width, uint y, uint z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint GetIndexBase(nuint depth, nuint width, nuint y, nuint z)
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

        public bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType)
        {
            return GetBlockStorage().TryGetInline(out inlineSpan, out storageType);
        }
    }
}
