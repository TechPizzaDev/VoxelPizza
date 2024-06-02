using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Fill<P, E, T>(
        this T tracker,
        Span<P> destination,
        nint start,
        nint count,
        E value,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return bitsPerElement switch
        {
            1 => Fill1(tracker, destination, start, count, value),
            2 => Fill2(tracker, destination, start, count, value),
            3 => Fill3(tracker, destination, start, count, value),
            4 => Fill4(tracker, destination, start, count, value),
            _ => FillN(tracker, destination, start, count, value, bitsPerElement),
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
    private static unsafe T FillCore<P, E, T>(
        T tracker,
        Span<P> destination,
        nint start,
        nint count,
        E value,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(E) * 8u);

        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint dstIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint dstLength = (nint)destination.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)dstLength);

        ref P dst = ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), dstIndex);

        tracker.Setup(bitsPerElement, elementsPerPart);

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
            P headPart = fullPart & dataMask;
            
            P prevPart = dst;
            P nextPart = prevPart & ~dataMask;
            nextPart |= headPart;

            tracker.PartChanged(prevPart, nextPart, bitsPerElement);
            dst = nextPart;

            dst = ref Unsafe.Add(ref dst, 1);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        if (T.ReportChanges)
        {
            for (nint j = 0; j < midCount; j++)
            {
                P prevPart = Unsafe.Add(ref dst, j);

                tracker.PartChanged(prevPart, fullPart, bitsPerElement);
                Unsafe.Add(ref dst, j) = fullPart;
            }
        }
        else
        {
            MemoryMarshal.CreateSpan(ref dst, (int)midCount).Fill(fullPart);
        }

        dst = ref Unsafe.Add(ref dst, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            int tailBitLen = (int)count * bitsPerElement;
            P clearMask = P.AllBitsSet << tailBitLen;
            P tailPart = fullPart & ~clearMask;
            
            P prevPart = dst;
            P nextPart = (prevPart & clearMask) | tailPart;

            tracker.PartChanged(prevPart, nextPart, bitsPerElement);
            dst = nextPart;
        }

        return tracker;
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
    private static T FillN<P, E, T>(
        T tracker, Span<P> destination, nint start, nint count, E value, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return FillCore(tracker, destination, start, count, value, bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Fill1<P, E, T>(T tracker, Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return FillCore(tracker, destination, start, count, value, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Fill2<P, E, T>(T tracker, Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return FillCore(tracker, destination, start, count, value, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Fill3<P, E, T>(T tracker, Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return FillCore(tracker, destination, start, count, value, 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Fill4<P, E, T>(T tracker, Span<P> destination, nint start, nint count, E value)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return FillCore(tracker, destination, start, count, value, 4);
    }
}
