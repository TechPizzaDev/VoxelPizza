using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using VoxelPizza.Collections;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Chunk : RefCounted, IBlockStorage
    {
        public BlockStorage0 EmptyStorage { get; } = new(Width, Height, Depth);

        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        private BlockStorage _storage;

        public event ChunkAction? Updated;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public ChunkRegion Region { get; }

        public BlockStorageType StorageType => _storage.StorageType;
        ushort IBlockStorage.Width => _storage.Width;
        ushort IBlockStorage.Height => _storage.Height;
        ushort IBlockStorage.Depth => _storage.Depth;
        public bool IsEmpty => _storage.IsEmpty;

        public Chunk(ChunkRegion region, ChunkPosition position)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Position = position;

            _storage = EmptyStorage;
        }

        public void InvokeUpdate()
        {
            Updated?.Invoke(this);
        }

        public BlockStorage GetBlockStorage()
        {
            if (_storage == EmptyStorage)
            {
                _storage = new BlockStorage8(Width, Height, Depth);
            }
            return _storage;
        }

        public void GetBlockRow(nuint index, ref uint destination, nuint length)
        {
            _storage.GetBlockRow(index, ref destination, length);
        }

        public void GetBlockRow(nuint x, nuint y, nuint z, ref uint destination, nuint length)
        {
            _storage.GetBlockRow(x, y, z, ref destination, length);
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

        public bool Generate()
        {
            int chunkX = X * Width;
            int chunkY = Y * Height;
            int chunkZ = Z * Depth;

            const int thresholdLow = (32 - 3) * 16;
            const int thresholdHigh = (32 - 2) * 16;

            int cDistSq = ((8 + chunkX) * (8 + chunkX)) + ((8 + chunkY) * (8 + chunkY)) + ((8 + chunkZ) * (8 + chunkZ));
            if (cDistSq <= (thresholdLow - 16) * (thresholdLow - 16) ||
                cDistSq >= (thresholdHigh + 16) * (thresholdHigh + 16))
            {
                return false;
            }

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
            XoshiroRandom rng = new(seed);

            if (true)
            {
                Span<byte> tmp = stackalloc byte[Width * Depth];
                Vector4 posOffset = new(chunkX, chunkY, chunkZ, 0);

                for (int y = 0; y < Height; y++)
                {
                    //double fac = (Y * Height + y) / 1024.0;
                    int blockY = chunkY + y;

                    rng.NextBytes(tmp);

                    for (int z = 0; z < Depth; z++)
                    {
                        int blockZ = chunkZ + z;
                        int indexBase = GetIndexBase(Depth, Width, y, z);
                        int distYZ_Sq = (y + chunkY) * (y + chunkY) + (z + chunkZ) * (z + chunkZ);

                        float cos = 0; // 64 * (MathF.Cos(blockZ / 16f) + 1) * 0.5f;

                        for (int x = 0; x < Width; x++)
                        {
                            uint v = 0;

                            int blockX = chunkX + x;
                            nint i = indexBase + x;

                            //ref uint block = ref Unsafe.Add(ref blockRef, i);
                            ref byte block8 = ref Unsafe.Add(ref blockRef8, i);
                            //ref ushort block16 = ref Unsafe.Add(ref blockRef16, i);

                            if (true)
                            {
                                int distSq = (x + chunkX) * (x + chunkX) + distYZ_Sq;
                                if (distSq > thresholdLow * thresholdLow &&
                                    distSq < thresholdHigh * thresholdHigh)
                                {
                                    v = (uint)tmp[x + z * Width] + 1;
                                    if (v > 255)
                                        v = 255;
                                }
                            }

                            if (false)
                            {
                                float sin = 64 * (MathF.Sin(blockX / 16f) + 1) * 0.5f;

                                if ((sin + cos) * 0.5f >= blockY)
                                    v = (uint)tmp[x + z * Width] + 1;
                            }

                            //block = v;
                            block8 = (byte)v;
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
            return true;
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

        private string GetDebuggerDisplay()
        {
            return $"{nameof(Chunk)}<{_storage.ToSimpleString()}>({Position.ToNumericString()})";
        }
    }
}
