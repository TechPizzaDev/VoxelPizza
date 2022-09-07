namespace VoxelPizza.World;

public abstract class TerrainGenerator
{
    public virtual bool CanGenerate(ChunkPosition position)
    {
        return true;
    }

    public abstract bool Generate(Chunk chunk);
}