using System;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Bits;

public struct BitTrackerAny<P> : IBitPartTracker<P>
    where P : IEquatable<P>
{
    public static bool ReportChanges => true;

    private bool _changed;

    public readonly bool IsChanged => _changed;

    public readonly nint ChangeCount => _changed ? 1 : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Setup(int bitsPerElement, int elementsPerPart)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PartChanged(P prev, P next, int bitsPerElement)
    {
        _changed |= !prev.Equals(next);
    }
}
