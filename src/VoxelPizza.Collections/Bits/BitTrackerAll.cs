using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Bits;

public struct BitTrackerAll<P> : IBitPartTracker<P>
    where P : unmanaged, IBinaryInteger<P>
{
    public static bool ReportChanges => true;

    private P _firstBitMask;

    private nint _changeCount;

    public readonly nint ChangeCount => _changeCount;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Setup(int bitsPerElement, int elementsPerPart)
    {
        P bitMask = P.Zero;
        for (int i = 0; i < elementsPerPart; i++)
        {
            bitMask <<= 1;
            bitMask |= P.One;
        }

        _firstBitMask = bitMask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PartChanged(P prev, P next, int bitsPerElement)
    {
        P difference = prev ^ next;
        if (difference == P.Zero)
        {
            // Skip if no bits changed. 
            // All bits changing is awfully unlikely, so no need to handle that specially.
            return;
        }

        // OR together all bits of elements,
        // since we only need to detect a single bit changing per element.
        P reduction = P.Zero;
        for (int i = 0; i < bitsPerElement; i++)
        {
            reduction |= difference & _firstBitMask;
            difference >>>= 1;
        }

        P count = P.PopCount(reduction);
        _changeCount += nint.CreateTruncating(count);
    }
}

