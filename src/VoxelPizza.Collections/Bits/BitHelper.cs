using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe E GetElementMask<E>(int bitsPerElement)
        where E : unmanaged, IBinaryInteger<E>
    {
        if (bitsPerElement == sizeof(E) * 8)
            return E.AllBitsSet;
        else
            return ~(E.AllBitsSet << bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetElementsPerPart<P>(int bitsPerElement)
        where P : unmanaged
    {
        int elementsPerPart = (sizeof(P) * 8) / bitsPerElement;
        return elementsPerPart;
    }

    public static nuint GetPartCount<P>(nuint elementCount, int bitsPerElement)
        where P : unmanaged
    {
        nuint elementsPerPart = (uint)GetElementsPerPart<P>(bitsPerElement);
        nuint longCount = (elementCount + elementsPerPart - 1) / elementsPerPart;
        return longCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static E Get<P, E>(ReadOnlySpan<P> source, int partIndex, int elementOffset, E elementMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        if ((uint)partIndex < (uint)source.Length)
        {
            P part = source[partIndex];
            E element = E.CreateTruncating(part >> elementOffset) & elementMask;
            return element;
        }
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set<P, E>(Span<P> destination, int partIndex, int elementOffset, E value, E elementMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        if ((uint)partIndex < (uint)destination.Length)
        {
            P part = destination[partIndex];
            P clearMask = P.CreateTruncating(elementMask) << elementOffset;
            P setMask = P.CreateTruncating(value & elementMask) << elementOffset;
            part &= ~clearMask;
            part |= setMask;
            destination[partIndex] = part;
        }
    }

}
