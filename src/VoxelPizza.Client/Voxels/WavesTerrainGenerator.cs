using System;
using VoxelPizza.Collections.Blocks;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public class WavesTerrainGenerator : TerrainGenerator
{
    public override bool CanGenerate(ChunkPosition position)
    {
        if (position.Y < -1)
        {
            return false;
        }
        if (position.Y > 6)
        {
            return false;
        }
        return true;
    }
    
    public override ChunkTicket CreateTicket(ValueArc<Chunk> chunk)
    {
        return new WavesTerrainTicket(chunk.Wrap());
    }

    public class WavesTerrainTicket : ChunkTicket
    {
        public WavesTerrainTicket(ValueArc<Chunk> chunk) : base(chunk.Wrap())
        {
        }

        public override GeneratorState Work(GeneratorState state)
        {
            if (state != GeneratorState.Complete)
            {
                return TransitionState(state);
            }

            if (IsStopRequested)
            {
                return State;
            }

            Chunk chunk = GetChunk().Get();
            ChunkPosition chunkPos = chunk.Position;

            BlockPosition blockPos = chunkPos.ToBlock();
            BlockStorage blockStorage = chunk.GetBlockStorage();

            Span<uint> layerTmp = stackalloc uint[Chunk.Width * Chunk.Depth];

            for (int y = 0; y < Chunk.Height; y++)
            {
                float blockY = blockPos.Y + y;

                if (IsStopRequested)
                {
                    return State;
                }

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

            return TransitionState(GeneratorState.Complete);
        }
    }
}
