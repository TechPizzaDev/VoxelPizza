using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public abstract class ChunkTicket : IStateMachine<GeneratorState, ValueArc<Chunk>>
{
    private int _state;

    private ValueArc<Chunk> _value;

    public GeneratorState State => (GeneratorState)_state;

    public bool IsStopRequested => _state == (int)GeneratorState.Dispose || _state == (int)GeneratorState.Cancel;

    public ValueArc<Chunk> Value => _value.Wrap();

    public ChunkTicket(ValueArc<Chunk> value)
    {
        _value = value.Track();

        TransitionState(GeneratorState.Idle);
    }

    protected virtual GeneratorState TransitionState(GeneratorState state)
    {
        GeneratorState previousState = (GeneratorState)Interlocked.Exchange(ref _state, (int)state);

        if (previousState == GeneratorState.Dispose)
        {
            ThrowDisposedException();
        }
        else if (state < previousState)
        {
            ThrowTransitionException(previousState, state);
        }

        if (state == GeneratorState.Dispose)
        {
            _value.Dispose();
        }

        return previousState;
    }

    public abstract GeneratorState Work(GeneratorState targetState);

    [DoesNotReturn]
    protected static void ThrowTransitionException(GeneratorState sourceState, GeneratorState targetState)
    {
        throw new InvalidOperationException($"Can not transition from {sourceState} to {targetState}.");
    }

    [DoesNotReturn]
    protected void ThrowDisposedException()
    {
        throw new ObjectDisposedException(GetType().Name);
    }
}