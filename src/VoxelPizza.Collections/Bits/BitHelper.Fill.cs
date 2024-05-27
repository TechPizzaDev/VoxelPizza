using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill<P, E>(
        Span<P> destination,
        nint start,
        nint count,
        E value,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        switch (bitsPerElement)
        {
            case 01: Fill1(destination, start, count, value); break;
            case 02: Fill2(destination, start, count, value); break;
            case 03: Fill3(destination, start, count, value); break;
            case 04: Fill4(destination, start, count, value); break;
            default: FillN(destination, start, count, value, bitsPerElement); break;
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe P FillBody<P, E>(E value, int count, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        P part = P.Zero;

        int rem = FillScalar(value, count, ref part, bitsPerElement);

        int insertShift = sizeof(P) * 8 - bitsPerElement;

        for (int i = count - rem; i < count; i++)
        {
            part >>>= bitsPerElement;
            part |= P.CreateTruncating(value) << insertShift;
        }

        return part;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void FillCore<P, E>(
        Span<P> destination,
        nint start,
        nint count,
        E value,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(E) * 8u);

        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint dstIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint dstLength = (nint)destination.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)dstLength);

        ref P dst = ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), dstIndex);

        P fullPart = FillBody<P, E>(value, elementsPerPart, bitsPerElement);

        if (startRem != 0)
        {
            int remElementsInPart = elementsPerPart - (int)startRem;
            int insertShiftHead = (int)startRem * bitsPerElement;

            int headCount = (int)count;
            if (headCount < remElementsInPart)
            {
                // Replacing portion of the part:
                //   x = existing bit, R = new bit, M = mid-point 
                //  part bits: [ xxRRRMxxxx ]
            }
            else
            {
                // Replacing everything after mid-point:
                //  part bits: [ RRRRRMxxxx ]
                headCount = remElementsInPart;
            }

            int headBitLen = headCount * bitsPerElement;
            P dataMask = ~(P.AllBitsSet << headBitLen) << insertShiftHead;
            dst &= ~dataMask;

            P headPart = fullPart & dataMask;
            dst |= headPart;

            dst = ref Unsafe.Add(ref dst, 1);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        for (nint j = 0; j < midCount; j++)
        {
            Unsafe.Add(ref dst, j) = fullPart;
        }

        dst = ref Unsafe.Add(ref dst, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            int tailBitLen = (int)count * bitsPerElement;
            P clearMask = P.AllBitsSet << tailBitLen;
            dst &= clearMask;

            P tailPart = fullPart & ~clearMask;
            dst |= tailPart;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int FillScalar<P, E>(E value, int count, ref P part, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int bitStride = 4 * bitsPerElement;
        int insertShift = sizeof(P) * 8 - bitStride;
        P trunc = P.CreateTruncating(value);

        while (count >= 4)
        {
            part >>>= bitStride;

            part |= trunc << (0 * bitsPerElement + insertShift);
            part |= trunc << (1 * bitsPerElement + insertShift);
            part |= trunc << (2 * bitsPerElement + insertShift);
            part |= trunc << (3 * bitsPerElement + insertShift);

            count -= 4;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FillN<P, E>(Span<P> destination, nint start, nint count, E value, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        FillCore(destination, start, count, value, bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fill1<P, E>(Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        FillCore(destination, start, count, value, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fill2<P, E>(Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        FillCore(destination, start, count, value, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fill3<P, E>(Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        FillCore(destination, start, count, value, 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fill4<P, E>(Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        FillCore(destination, start, count, value, 4);
    }
}
