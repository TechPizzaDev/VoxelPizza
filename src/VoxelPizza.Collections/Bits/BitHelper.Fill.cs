using System;
using System.Numerics;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    public static void Fill<P, E>(
        Span<P> destination,
        nint start,
        nint count,
        E value,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        E elementMask = GetElementMask<E>(bitsPerElement);

        // TODO: optimize

        for (nint i = 0; i < count; i++)
        {
            (int part, int element) = Math.DivRem((int)(i + start), elementsPerPart);
            Set(destination, part, element * bitsPerElement, value, elementMask);
        }
    }
}
