using VoxelPizza.Collections;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public class FillTerrainGenerator : TerrainGenerator
{
    public uint Value;

    public override bool CanGenerate(ChunkPosition position)
    {
        return true;
    }

    public override ChunkTicket CreateTicket(ValueArc<Chunk> chunk)
    {
        return new FillTerrainTicket(chunk.Wrap(), this);
    }

    public class FillTerrainTicket : ChunkTicket
    {
        public FillTerrainGenerator Generator { get; }

        public FillTerrainTicket(ValueArc<Chunk> chunk, FillTerrainGenerator generator) : base(chunk.Wrap())
        {
            Generator = generator;
        }

        public override GeneratorState Work(GeneratorState state)
        {
            if (state != GeneratorState.Complete)
            {
                return TransitionState(state);
            }

            if (Generator.Value != 0)
            {
                Chunk chunk = GetChunk().Get();

                BlockStorage blockStorage = chunk.GetBlockStorage();

                blockStorage.FillBlock(Generator.Value);
            }

            return TransitionState(GeneratorState.Complete);
        }
    }
}