using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using VoxelPizza.Numerics;

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
    private static void UnpackPart<P, E>(ref E dst, P part, int count, E mask, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int i = 0;

        if (Unsafe.SizeOf<P>() == sizeof(ulong) && Unsafe.SizeOf<E>() == sizeof(uint) && count >= 8)
        {
            ref uint iDst = ref Unsafe.As<E, uint>(ref dst);
            ulong iPart = Unsafe.BitCast<P, ulong>(part);
            int rem = count;

            if (bitsPerElement == 1)
            {
                rem = Unpack1Special(ref iDst, iPart, count);
            }

            i = count - rem;
            part >>= i * bitsPerElement;
        }

        for (; i < count; i++)
        {
            E element = E.CreateTruncating(part) & mask;
            part >>= bitsPerElement;
            Unsafe.Add(ref dst, i) = element;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void UnpackCore<P, E>(Span<E> destination, ReadOnlySpan<P> source, nint start, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint srcIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint count = destination.Length;
        nint srcLength = (nint)source.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)srcLength);

        ref P src = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), srcIndex);
        ref E dst = ref MemoryMarshal.GetReference(destination);

        E elementMask = GetElementMask<E>(bitsPerElement);

        if (startRem != 0)
        {
            int headCount = Math.Min(elementsPerPart - (int)startRem, (int)count);
            int headOffset = (int)startRem * bitsPerElement;
            P headPart = src >> headOffset;
            UnpackPart(ref dst, headPart, headCount, elementMask, bitsPerElement);

            dst = ref Unsafe.Add(ref dst, headCount);
            src = ref Unsafe.Add(ref src, 1);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        for (nint j = 0; j < midCount; j++)
        {
            P part = Unsafe.Add(ref src, j);
            UnpackPart(ref dst, part, elementsPerPart, elementMask, bitsPerElement);

            dst = ref Unsafe.Add(ref dst, elementsPerPart);
        }

        src = ref Unsafe.Add(ref src, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            P tailPart = src;
            UnpackPart(ref dst, tailPart, (int)count, elementMask, bitsPerElement);
        }
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

    private static int Unpack1Special(ref uint dst, ulong part, int count)
    {
        const uint mask = 0b1;

        if (Avx512F.IsSupported)
        {
            while (count >= 16)
            {
                uint quarter = (uint)part;
                part >>= 16;

                Vector512<uint> vh = Vector512.Create(quarter);
                Vector512<uint> vm = Vector512.Create(mask);

                Vector512<uint> sc = Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15u);
                Vector512<uint> v0 = Avx512F.ShiftRightLogicalVariable(vh, sc);
                (v0 & vm).StoreUnsafe(ref dst);

                dst = ref Unsafe.Add(ref dst, 16);
                count -= 16;
            }
        }

        while (count >= 8)
        {
            uint octal = (uint)part;
            part >>= 8;

            if (Avx2.IsSupported)
            {
                Vector256<uint> vh = Vector256.Create(octal);
                Vector256<uint> vm = Vector256.Create(mask);

                Vector256<uint> v0 = Avx2.ShiftRightLogicalVariable(vh, Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7u));
                (v0 & vm).StoreUnsafe(ref dst);
            }
            else if (V128Helper.IsVariableShiftAccelerated)
            {
                Vector128<uint> vh = Vector128.Create(octal);
                Vector128<uint> vm = Vector128.Create(mask);

                Vector128<uint> v0 = V128Helper.ShiftRightLogical(vh, Vector128.Create(0, 1, 2, 3u));
                (v0 & vm).StoreUnsafe(ref dst, 0);
                Vector128<uint> v1 = V128Helper.ShiftRightLogical(vh, Vector128.Create(4, 5, 6, 7u));
                (v1 & vm).StoreUnsafe(ref dst, 4);
            }
            else
            {
                Unsafe.Add(ref dst, 00) = (octal >> 00) & mask;
                Unsafe.Add(ref dst, 01) = (octal >> 01) & mask;
                Unsafe.Add(ref dst, 02) = (octal >> 02) & mask;
                Unsafe.Add(ref dst, 03) = (octal >> 03) & mask;
                Unsafe.Add(ref dst, 04) = (octal >> 04) & mask;
                Unsafe.Add(ref dst, 05) = (octal >> 05) & mask;
                Unsafe.Add(ref dst, 06) = (octal >> 06) & mask;
                Unsafe.Add(ref dst, 07) = (octal >> 07) & mask;
            }

            dst = ref Unsafe.Add(ref dst, 8);
            count -= 8;
        }

        return count;
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
