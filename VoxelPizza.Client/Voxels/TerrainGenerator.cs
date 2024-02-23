using VoxelPizza.Memory;

namespace VoxelPizza.World;

public abstract class TerrainGenerator
{
    public virtual bool CanGenerate(ChunkPosition position)
    {
        return true;
    }

    public abstract ChunkTicket CreateTicket(ValueArc<Chunk> chunk);
}
