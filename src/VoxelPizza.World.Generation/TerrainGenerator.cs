using VoxelPizza.Memory;

namespace VoxelPizza.World;

public abstract class TerrainGenerator
{
    public virtual bool CanGenerate(ChunkPosition position)
    {
        return true;
    }

    public abstract ChunkTicket? CreateTicket(ValueArc<Chunk> chunk);

    public virtual ChunkTicket? CreateTicket(ValueArc<Dimension> dimension, ChunkPosition chunkPosition)
    {
        using ValueArc<Chunk> chunk = Dimension.CreateChunk(dimension, chunkPosition, out _);
        return CreateTicket(chunk);
    }

    public virtual ChunkTicket? CreateTicket(ValueArc<ChunkRegion> chunkRegion, ChunkPosition chunkPosition)
    {
        using ValueArc<Chunk> chunk = ChunkRegion.CreateChunk(chunkRegion, chunkPosition, out _);
        return CreateTicket(chunk);
    }
}
