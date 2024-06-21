using System;
using VoxelPizza.Collections.Blocks;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public class SphereTerrainGenerator : TerrainGenerator
{
    public int ThresholdLow = (32 - 3) * 16;
    public int ThresholdHigh = (32 - 2) * 16;

    public override bool CanGenerate(ChunkPosition position)
    {
        BlockPosition blockPos = position.ToBlock();

        int cDistSq = (
            (8 + blockPos.X) * (8 + blockPos.X)) +
            ((8 + blockPos.Y) * (8 + blockPos.Y)) +
            ((8 + blockPos.Z) * (8 + blockPos.Z));

        if (cDistSq <= (ThresholdLow - 16) * (ThresholdLow - 16) ||
            cDistSq >= (ThresholdHigh + 16) * (ThresholdHigh + 16))
        {
            return false;
        }

        return true;
    }

    public override ChunkTicket CreateTicket(ValueArc<Chunk> chunk)
    {
        return new SphereTerrainTicket(chunk.Wrap(), this);
    }

    public class SphereTerrainTicket : ChunkTicket
    {
        public SphereTerrainGenerator Generator { get; }

        public SphereTerrainTicket(ValueArc<Chunk> chunk, SphereTerrainGenerator generator) : base(chunk.Wrap())
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public override GeneratorState Work(GeneratorState state)
        {
            if (state != GeneratorState.Complete)
            {
                return TransitionState(state);
            }

            Chunk chunk = GetChunk().Get();
            ChunkPosition chunkPos = chunk.Position;

            BlockPosition blockPos = chunkPos.ToBlock();
            BlockStorage blockStorage = chunk.GetBlockStorage();

            int threshLow_Sq = Generator.ThresholdLow * Generator.ThresholdLow;
            int threshHigh_Sq = Generator.ThresholdHigh * Generator.ThresholdHigh;

            for (int y = 0; y < Chunk.Height; y++)
            {
                int distY_Sq = (y + blockPos.Y) * (y + blockPos.Y);

                for (int z = 0; z < Chunk.Depth; z++)
                {
                    int distYZ_Sq = distY_Sq + (z + blockPos.Z) * (z + blockPos.Z);

                    for (int x = 0; x < Chunk.Width; x++)
                    {
                        int distSq = distYZ_Sq + (x + blockPos.X) * (x + blockPos.X);

                        if (distSq > threshLow_Sq &&
                            distSq < threshHigh_Sq)
                        {
                            blockStorage.SetBlock(x, y, z, 1);
                        }
                    }
                }
            }

            return TransitionState(GeneratorState.Complete);
        }
    }
}