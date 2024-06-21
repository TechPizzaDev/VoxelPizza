namespace VoxelPizza.World;

public enum GeneratorState
{
    Idle,
    Enqueue,

    Init,
    Work,

    Complete,
    Cancel,
    Dispose,
}
