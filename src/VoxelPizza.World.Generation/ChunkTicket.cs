using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public abstract class ChunkTicket : IStateMachine<GeneratorState>
{
    private int _state;

    private ValueArc<Chunk> _chunk;

    public GeneratorState State => (GeneratorState)_state;

    public bool IsStopRequested => _state == (int)GeneratorState.Dispose || _state == (int)GeneratorState.Cancel;

    public ChunkTicket(ValueArc<Chunk> chunk)
    {
        _chunk = chunk.Track();

        TransitionState(GeneratorState.Idle);
    }

    public ValueArc<Chunk> GetChunk() => _chunk.Wrap();

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
            _chunk.Dispose();
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