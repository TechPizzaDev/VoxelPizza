using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Bits;

public readonly struct BitTrackerNone<P> : IBitPartTracker<P>
{
    public static bool ReportChanges => false;

    public nint ChangeCount => 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Setup(int bitsPerElement, int elementsPerPart) { }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PartChanged(P prev, P next, int bitsPerElement) { }
}
