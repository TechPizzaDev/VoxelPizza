using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Pack<P, E, T>(
        this T tracker,
        Span<P> destination,
        nint start,
        ReadOnlySpan<E> source,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return bitsPerElement switch
        {
            1 => Pack1(tracker, destination, start, source),
            2 => Pack2(tracker, destination, start, source),
            3 => Pack3(tracker, destination, start, source),
            4 => Pack4(tracker, destination, start, source),
            _ => PackN(tracker, destination, start, source, bitsPerElement),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe P PackBody<P, E>(ref E src, int count, int bitsPerElement, P extractMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        P part = P.Zero;

        int rem;
        if (Bmi2.X64.IsSupported && UseBmi2X64<E>())
        {
            rem = PackBmi2X64(ref src, count, ref part, bitsPerElement, extractMask);
        }
        else if (Bmi2.IsSupported && UseBmi2<E>())
        {
            rem = PackBmi2(ref src, count, ref part, bitsPerElement, extractMask);
        }
        else
        {
            rem = PackScalar(ref src, count, ref part, bitsPerElement);
        }

        int insertShift = sizeof(P) * 8 - bitsPerElement;

        for (int i = count - rem; i < count; i++)
        {
            E element = Unsafe.Add(ref src, i);
            part >>>= bitsPerElement;
            part |= P.CreateTruncating(element) << insertShift;
        }

        return part;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe T PackCore<P, E, T>(
        T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(E) * 8u);

        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint dstIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint count = source.Length;
        nint dstLength = (nint)destination.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)dstLength);

        ref E src = ref MemoryMarshal.GetReference(source);
        ref P dst = ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), dstIndex);

        E elementMask = GetElementMask<E>(bitsPerElement);
        P extractMask = GetParallelMask<P, E>(elementMask);

        tracker.Setup(bitsPerElement, elementsPerPart);

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

            P headPart = PackBody(ref src, headCount, bitsPerElement, extractMask);
            headPart >>>= (sizeof(P) * 8 - headBitLen) - insertShiftHead;

            P prevPart = dst;
            P nextPart = (prevPart & ~dataMask) | headPart;

            tracker.PartChanged(prevPart, nextPart, bitsPerElement);
            dst = nextPart;

            dst = ref Unsafe.Add(ref dst, 1);
            src = ref Unsafe.Add(ref src, headCount);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        for (nint j = 0; j < midCount; j++)
        {
            P prevPart = Unsafe.Add(ref dst, j);
            P nextPart = PackBody(ref src, elementsPerPart, bitsPerElement, extractMask);

            tracker.PartChanged(prevPart, nextPart, bitsPerElement);
            Unsafe.Add(ref dst, j) = nextPart;

            src = ref Unsafe.Add(ref src, elementsPerPart);
        }

        dst = ref Unsafe.Add(ref dst, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            int tailBitLen = (int)count * bitsPerElement;
            P clearMask = P.AllBitsSet << tailBitLen;

            P tailPart = PackBody(ref src, (int)count, bitsPerElement, extractMask);
            tailPart >>>= sizeof(P) * 8 - tailBitLen;

            P prevPart = dst;
            P nextPart = (prevPart & clearMask) | tailPart;

            tracker.PartChanged(prevPart, nextPart, bitsPerElement);
            dst = nextPart;
        }

        return tracker;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int PackBmi2X64<P, E>(ref E src, int count, ref P part, int bitsPerElement, P extractMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int stride = Math.Min(sizeof(ulong), sizeof(P)) / sizeof(E);
        int bitStride = stride * bitsPerElement;
        int insertShift = sizeof(P) * 8 - bitStride;

        while (count >= stride)
        {
            ulong data = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<E, byte>(ref src));
            ulong mask = Bmi2.X64.ParallelBitExtract(data, ulong.CreateTruncating(extractMask));

            part >>>= bitStride;
            part |= P.CreateTruncating(mask) << insertShift;

            src = ref Unsafe.Add(ref src, stride);
            count -= stride;
        }

        if (UseBmi2<E>())
        {
            // `Bmi2.IsSupported` must be true if `Bmi2.X64.IsSupported` is true.
            count = PackBmi2(ref src, count, ref part, bitsPerElement, extractMask);
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int PackBmi2<P, E>(ref E src, int count, ref P part, int bitsPerElement, P extractMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int stride = Math.Min(sizeof(uint), sizeof(P)) / sizeof(E);
        int bitStride = stride * bitsPerElement;
        int insertShift = sizeof(P) * 8 - bitStride;

        while (count >= stride)
        {
            uint data = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<E, byte>(ref src));
            uint mask = Bmi2.ParallelBitExtract(data, uint.CreateTruncating(extractMask));

            part >>>= bitStride;
            part |= P.CreateTruncating(mask) << insertShift;

            src = ref Unsafe.Add(ref src, stride);
            count -= stride;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int PackScalar<P, E>(ref E src, int count, ref P part, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int bitStride = 8 * bitsPerElement;
        int insertShift = sizeof(P) * 8 - bitStride;

        while (count >= 8)
        {
            P od = P.Zero;
            P ev = P.Zero;

            od |= P.CreateTruncating(Unsafe.Add(ref src, 0)) << (0 * bitsPerElement + insertShift);
            ev |= P.CreateTruncating(Unsafe.Add(ref src, 1)) << (1 * bitsPerElement + insertShift);
            od |= P.CreateTruncating(Unsafe.Add(ref src, 2)) << (2 * bitsPerElement + insertShift);
            ev |= P.CreateTruncating(Unsafe.Add(ref src, 3)) << (3 * bitsPerElement + insertShift);
            od |= P.CreateTruncating(Unsafe.Add(ref src, 4)) << (4 * bitsPerElement + insertShift);
            ev |= P.CreateTruncating(Unsafe.Add(ref src, 5)) << (5 * bitsPerElement + insertShift);
            od |= P.CreateTruncating(Unsafe.Add(ref src, 6)) << (6 * bitsPerElement + insertShift);
            ev |= P.CreateTruncating(Unsafe.Add(ref src, 7)) << (7 * bitsPerElement + insertShift);

            part >>>= bitStride;
            part |= od;
            part |= ev;

            src = ref Unsafe.Add(ref src, 8);
            count -= 8;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T PackN<P, E, T>(
        T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return PackCore(tracker, destination, start, source, bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Pack1<P, E, T>(T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return PackCore(tracker, destination, start, source, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Pack2<P, E, T>(T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return PackCore(tracker, destination, start, source, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Pack3<P, E, T>(T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return PackCore(tracker, destination, start, source, 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T Pack4<P, E, T>(T tracker, Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
        where T : struct, IBitPartTracker<P>
    {
        return PackCore(tracker, destination, start, source, 4);
    }
}
