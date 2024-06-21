
namespace VoxelPizza.World
{
    public interface IStateMachine<TState>
    {
        TState State { get; }

        TState Work(TState state);
    }
}
