using System;
using VoxelPizza.Collections;

namespace VoxelPizza.World;

public class WavesTerrainGenerator : TerrainGenerator
{
    public override bool CanGenerate(ChunkPosition position)
    {
        if (position.Y < 0)
        {
            return false;
        }
        if (position.Y > 4)
        {
            return false;
        }
        return true;
    }

    public override bool Generate(Chunk chunk)
    {
        ChunkPosition chunkPos = chunk.Position;
        if (!CanGenerate(chunk.Position))
        {
            return false;
        }

        BlockPosition blockPos = chunkPos.ToBlock();
        BlockStorage blockStorage = chunk.GetBlockStorage();

        Span<uint> layerTmp = stackalloc uint[Chunk.Width * Chunk.Depth];

        for (int y = 0; y < Chunk.Height; y++)
        {
            float blockY = blockPos.Y + y;

            for (int z = 0; z < Chunk.Depth; z++)
            {
                float blockZ = blockPos.Z + z;
                float cos = 64 * (MathF.Cos(blockZ / 16f) + 1) * 0.5f;

                if (cos * 0.5f >= blockY)
                {
                    for (int x = 0; x < Chunk.Width; x++)
                    {
                        layerTmp[x + z * Chunk.Width] = 1;
                    }
                    continue;
                }

                for (int x = 0; x < Chunk.Width; x++)
                {
                    float blockX = blockPos.X + x;
                    float sin = 64 * (MathF.Sin(blockX / 16f) + 1) * 0.5f;

                    uint v = 0;
                    if ((sin + cos) * 0.5f >= blockY)
                    {
                        v = 1;
                    }
                    layerTmp[x + z * Chunk.Width] = v;
                }
            }

            blockStorage.SetBlockLayer(y, layerTmp);
        }
        return true;
    }
}