namespace VoxelPizza.World;

public enum GeneratorState
{
    Idle,
    Enqueue,
    Work,
    Complete,
    Cancel,
    Dispose,
}
