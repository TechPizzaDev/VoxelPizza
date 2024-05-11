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
    private static unsafe void UnpackPart<P, E>(ref E dst, P part, int count, E mask, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int i = 0;

        if (count >= 4)
        {
            int rem = count;

            if (bitsPerElement == 1)
            {
                rem = Unpack1Special(ref dst, part, count);
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(E) * 8u);

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

    private static unsafe int Unpack1Special<P, E>(ref E dst, P part, int count)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        if (Bmi2.X64.IsSupported && sizeof(E) == 1)
        {
            while (count >= 8)
            {
                ulong mask = Bmi2.X64.ParallelBitDeposit(ulong.CreateTruncating(part), 0x01_01_01_01_01_01_01_01);
                part >>= 8;
                Unsafe.WriteUnaligned(ref Unsafe.As<E, byte>(ref dst), mask);

                dst = ref Unsafe.Add(ref dst, 8);
                count -= 8;
            }
        }

        if (Bmi2.IsSupported && sizeof(E) == 1)
        {
            while (count >= 4)
            {
                uint mask = Bmi2.ParallelBitDeposit(uint.CreateTruncating(part), 0x01_01_01_01);
                part >>= 4;
                Unsafe.WriteUnaligned(ref Unsafe.As<E, byte>(ref dst), mask);

                dst = ref Unsafe.Add(ref dst, 4);
                count -= 4;
            }

            return count;
        }

        E elementMask = E.CreateTruncating(0b1);
        
        if (V128Helper.IsAcceleratedShiftRightLogical<E>() && sizeof(E) >= 2)
        {
            while (count >= Vector128<E>.Count)
            {
                E slice = E.CreateTruncating(part);
                part >>= Vector128<E>.Count;

                Vector128<E> vh = Vector128.Create(slice);
                Vector128<E> vm = Vector128.Create(elementMask);

                Vector128<E> sc = V128Helper.CreateIncrement(E.Zero, E.One);
                Vector128<E> v0 = V128Helper.ShiftRightLogical(vh, sc);
                (v0 & vm).StoreUnsafe(ref dst);

                dst = ref Unsafe.Add(ref dst, Vector128<E>.Count);
                count -= Vector128<E>.Count;
            }
        }

        if (V64Helper.IsAcceleratedShiftRightLogical<E>())
        {
            while (count >= Vector64<E>.Count)
            {
                E slice = E.CreateTruncating(part);
                part >>= Vector64<E>.Count;

                Vector64<E> vh = Vector64.Create(slice);
                Vector64<E> vm = Vector64.Create(elementMask);

                Vector64<E> sc = V64Helper.CreateIncrement(E.Zero, E.One);
                Vector64<E> v0 = V64Helper.ShiftRightLogical(vh, sc);
                (v0 & vm).StoreUnsafe(ref dst);

                dst = ref Unsafe.Add(ref dst, Vector64<E>.Count);
                count -= Vector64<E>.Count;
            }
        }

        while (count >= 8)
        {
            E slice = E.CreateTruncating(part);
            part >>= 8;

            Unsafe.Add(ref dst, 0) = (slice >> 0) & elementMask;
            Unsafe.Add(ref dst, 1) = (slice >> 1) & elementMask;
            Unsafe.Add(ref dst, 2) = (slice >> 2) & elementMask;
            Unsafe.Add(ref dst, 3) = (slice >> 3) & elementMask;
            Unsafe.Add(ref dst, 4) = (slice >> 4) & elementMask;
            Unsafe.Add(ref dst, 5) = (slice >> 5) & elementMask;
            Unsafe.Add(ref dst, 6) = (slice >> 6) & elementMask;
            Unsafe.Add(ref dst, 7) = (slice >> 7) & elementMask;

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
