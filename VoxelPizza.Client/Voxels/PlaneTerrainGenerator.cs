
using System;
using VoxelPizza.Collections;
using VoxelPizza.Numerics;

namespace VoxelPizza.World;

public class PlaneTerrainGenerator : TerrainGenerator
{
    public int LevelY;

    public override bool CanGenerate(ChunkPosition position)
    {
        return position.Y == LevelY;
    }

    public override bool Generate(Chunk chunk)
    {
        ChunkPosition chunkPos = chunk.Position;
        if (!CanGenerate(chunk.Position))
        {
            return false;
        }

        BlockStorage blockStorage = chunk.GetBlockStorage();

        ulong seed = 17;
        seed = seed * 31 + (uint)chunkPos.X;
        seed = seed * 31 + (uint)chunkPos.Y;
        seed = seed * 31 + (uint)chunkPos.Z;
        XoshiroRandom rng = new(seed);

        Span<byte> tmp8 = stackalloc byte[Chunk.Width];
        Span<uint> tmp32 = stackalloc uint[Chunk.Width];

        for (int z = 0; z < Chunk.Depth; z++)
        {
            rng.NextBytes(tmp8);
            BlockStorage.Expand8To32(tmp8, tmp32, Chunk.Width);
            blockStorage.SetBlockRow(0, 0, z, tmp32);
        }

        return true;
    }
}

/*

public class SphereTerrainGenerator : TerrainGenerator
{
    public override bool Generate(Chunk chunk)
    {
        int chunkX = X * Width;
        int chunkY = Y * Height;
        int chunkZ = Z * Depth;

        const int thresholdLow = (32 - 3) * 16;
        const int thresholdHigh = (32 - 2) * 16;

        if (type == GenerationType.Sphere)
        {
            int cDistSq = ((8 + chunkX) * (8 + chunkX)) + ((8 + chunkY) * (8 + chunkY)) + ((8 + chunkZ) * (8 + chunkZ));
            if (cDistSq <= (thresholdLow - 16) * (thresholdLow - 16) ||
                cDistSq >= (thresholdHigh + 16) * (thresholdHigh + 16))
            {
                return false;
            }
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

        if (type == GenerationType.Plane)
        {
            rng.NextBytes(blocks8.Slice(0, Width * Depth));
            return true;
        }

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

                float cos = type == GenerationType.Waves
                    ? 64 * (MathF.Cos(blockZ / 16f) + 1) * 0.5f
                    : 0;

                for (int x = 0; x < Width; x++)
                {
                    uint v = 0;

                    int blockX = chunkX + x;
                    nint i = indexBase + x;

                    //ref uint block = ref Unsafe.Add(ref blockRef, i);
                    ref byte block8 = ref Unsafe.Add(ref blockRef8, i);
                    //ref ushort block16 = ref Unsafe.Add(ref blockRef16, i);

                    if (type == GenerationType.Sphere)
                    {
                        int distSq = (x + chunkX) * (x + chunkX) + distYZ_Sq;
                        if (distSq > thresholdLow * thresholdLow &&
                            distSq < thresholdHigh * thresholdHigh)
                        {
                            v = (uint)tmp[x + z * Width] + 1;
                        }
                    }
                    else if (type == GenerationType.Waves)
                    {
                        float sin = 64 * (MathF.Sin(blockX / 16f) + 1) * 0.5f;

                        if ((sin + cos) * 0.5f >= blockY)
                            v = (uint)tmp[x + z * Width] + 1;
                    }

                    if (v > 255)
                        v = 255;

                    //block = v;
                    block8 = (byte)v;
                    //block16 = v > ushort.MaxValue ? ushort.MaxValue : (ushort)v;
                }
            }
        }
        return true;
    }
}

*/