
namespace VoxelPizza.World
{
    public interface IStateMachine<TState>
    {
        TState State { get; }

        TState Work(TState state);
    }

    public interface IStateMachine<TState, TValue> : IStateMachine<TState>
    {
        TValue Value { get; }
    }
}
