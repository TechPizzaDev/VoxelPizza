using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unpack<P, E>(
        Span<E> destination,
        ReadOnlySpan<P> source,
        nint start,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        switch (bitsPerElement)
        {
            case 01: Unpack1(destination, source, start); break;
            case 02: Unpack2(destination, source, start); break;
            case 03: Unpack3(destination, source, start); break;
            case 04: Unpack4(destination, source, start); break;
            default: UnpackN(destination, source, start, bitsPerElement); break;
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void UnpackPart<P, E>(ref E dst, P part, int count, E elementMask, int bitsPerElement, P depositMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int rem;
        if (Bmi2.X64.IsSupported && UseBmi2X64<E>())
        {
            rem = UnpackBmi2X64(ref dst, part, count, bitsPerElement, depositMask);
        }
        else if (Bmi2.IsSupported && UseBmi2<E>())
        {
            rem = UnpackBmi2(ref dst, part, count, bitsPerElement, depositMask);
        }
        else
        {
            rem = UnpackScalar(ref dst, part, count, elementMask, bitsPerElement);
        }

        int i = count - rem;
        part >>>= i * bitsPerElement;

        for (; i < count; i++)
        {
            E element = E.CreateTruncating(part) & elementMask;
            part >>>= bitsPerElement;
            Unsafe.Add(ref dst, i) = element;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void UnpackCore<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(E) * 8u);

        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint srcIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint count = destination.Length;
        nint srcLength = (nint)source.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)srcLength);

        ref P src = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), srcIndex);
        ref E dst = ref MemoryMarshal.GetReference(destination);

        E elementMask = GetElementMask<E>(bitsPerElement);
        P depositMask = GetParallelMask<P, E>(elementMask);

        if (startRem != 0)
        {
            int headCount = Math.Min(elementsPerPart - (int)startRem, (int)count);
            int headOffset = (int)startRem * bitsPerElement;
            P headPart = src >>> headOffset;
            UnpackPart(ref dst, headPart, headCount, elementMask, bitsPerElement, depositMask);

            dst = ref Unsafe.Add(ref dst, headCount);
            src = ref Unsafe.Add(ref src, 1);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        for (nint j = 0; j < midCount; j++)
        {
            P part = Unsafe.Add(ref src, j);
            UnpackPart(ref dst, part, elementsPerPart, elementMask, bitsPerElement, depositMask);

            dst = ref Unsafe.Add(ref dst, elementsPerPart);
        }

        src = ref Unsafe.Add(ref src, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            P tailPart = src;
            UnpackPart(ref dst, tailPart, (int)count, elementMask, bitsPerElement, depositMask);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int UnpackBmi2X64<P, E>(ref E dst, P part, int count, int bitsPerElement, P depositMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int stride = Math.Min(sizeof(ulong), sizeof(P)) / sizeof(E);

        while (count >= stride)
        {
            ulong mask = Bmi2.X64.ParallelBitDeposit(ulong.CreateTruncating(part), ulong.CreateTruncating(depositMask));
            Unsafe.WriteUnaligned(ref Unsafe.As<E, byte>(ref dst), mask);

            dst = ref Unsafe.Add(ref dst, stride);
            part >>>= stride * bitsPerElement;
            count -= stride;
        }

        if (UseBmi2<E>())
        {
            // `Bmi2.IsSupported` must be true if `Bmi2.X64.IsSupported` is true.
            count = UnpackBmi2(ref dst, part, count, bitsPerElement, depositMask);
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int UnpackBmi2<P, E>(ref E dst, P part, int count, int bitsPerElement, P depositMask)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int stride = Math.Min(sizeof(uint), sizeof(P)) / sizeof(E);

        while (count >= stride)
        {
            uint mask = Bmi2.ParallelBitDeposit(uint.CreateTruncating(part), uint.CreateTruncating(depositMask));
            Unsafe.WriteUnaligned(ref Unsafe.As<E, byte>(ref dst), mask);

            dst = ref Unsafe.Add(ref dst, stride);
            part >>>= stride * bitsPerElement;
            count -= stride;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UnpackScalar<P, E>(ref E dst, P part, int count, E elementMask, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        while (count >= 8)
        {
            Unsafe.Add(ref dst, 0) = E.CreateTruncating(part >>> (0 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 1) = E.CreateTruncating(part >>> (1 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 2) = E.CreateTruncating(part >>> (2 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 3) = E.CreateTruncating(part >>> (3 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 4) = E.CreateTruncating(part >>> (4 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 5) = E.CreateTruncating(part >>> (5 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 6) = E.CreateTruncating(part >>> (6 * bitsPerElement)) & elementMask;
            Unsafe.Add(ref dst, 7) = E.CreateTruncating(part >>> (7 * bitsPerElement)) & elementMask;

            dst = ref Unsafe.Add(ref dst, 8);
            part >>>= 8 * bitsPerElement;
            count -= 8;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnpackN<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        UnpackCore(destination, source, start, bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unpack1<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        UnpackCore(destination, source, start, 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unpack2<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        UnpackCore(destination, source, start, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unpack3<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        UnpackCore(destination, source, start, 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unpack4<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        UnpackCore(destination, source, start, 4);
    }
}
