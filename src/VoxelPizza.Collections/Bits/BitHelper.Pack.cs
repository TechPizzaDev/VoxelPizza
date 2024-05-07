using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pack<P, E>(
        Span<P> destination,
        nint start,
        ReadOnlySpan<E> source,
        int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        switch (bitsPerElement)
        {
            case 01: Pack1(destination, start, source); break;
            case 02: Pack2(destination, start, source); break;
            case 03: Pack3(destination, start, source); break;
            case 04: Pack4(destination, start, source); break;
            default: PackN(destination, start, source, bitsPerElement); break;
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe P PackBody<P, E>(ref E src, int count, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        P part = P.Zero;
        int i = 0;

        if (Unsafe.SizeOf<P>() == sizeof(ulong) && Unsafe.SizeOf<E>() == sizeof(uint) && count >= 8)
        {
            ref uint iSrc = ref Unsafe.As<E, uint>(ref src);
            ulong iPart = 0;
            int rem = count;

            if (bitsPerElement == 1)
            {
                iPart = Pack1Special(ref iSrc, count, out rem);
            }

            i = count - rem;
            int insertShiftPart = sizeof(P) * 8 - i * bitsPerElement;
            part = P.CreateChecked(iPart << insertShiftPart);
        }

        int insertShiftElem = sizeof(P) * 8 - bitsPerElement;
        for (; i < count; i++)
        {
            E element = Unsafe.Add(ref src, i);
            part >>= bitsPerElement;
            part |= P.CreateTruncating(element) << insertShiftElem;
        }

        return part;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void PackCore<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        int elementsPerPart = GetElementsPerPart<P>(bitsPerElement);
        (nint dstIndex, nint startRem) = Math.DivRem(start, elementsPerPart);

        nint count = source.Length;
        nint dstLength = (nint)destination.Length * elementsPerPart - start;
        ArgumentOutOfRangeException.ThrowIfGreaterThan((nuint)count, (nuint)dstLength);

        ref E src = ref MemoryMarshal.GetReference(source);
        ref P dst = ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), dstIndex);

        if (startRem != 0)
        {
            int headCount = Math.Min(elementsPerPart - (int)startRem, (int)count);
            P headPart = PackBody<P, E>(ref src, headCount, bitsPerElement);
            int insertShiftHead = (int)startRem * bitsPerElement;
            headPart <<= insertShiftHead;
            dst &= ~headPart;
            dst |= headPart;

            dst = ref Unsafe.Add(ref dst, 1);
            src = ref Unsafe.Add(ref src, headCount);
            count -= headCount;
        }

        nint midCount = count / elementsPerPart;
        for (nint j = 0; j < midCount; j++)
        {
            P part = PackBody<P, E>(ref src, elementsPerPart, bitsPerElement);
            Unsafe.Add(ref dst, j) = part;

            src = ref Unsafe.Add(ref src, elementsPerPart);
        }

        dst = ref Unsafe.Add(ref dst, midCount);
        count -= midCount * elementsPerPart;

        if (count > 0)
        {
            P tailPart = PackBody<P, E>(ref src, (int)count, bitsPerElement);
            dst &= ~tailPart;
            dst |= tailPart;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PackN<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source, int bitsPerElement)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        PackCore(destination, start, source, bitsPerElement);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Pack1<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        PackCore(destination, start, source, 1);
    }

    private static ulong Pack1Special(ref uint src, int count, out int rem)
    {
        ulong part = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            while (count >= 16)
            {
                ref int data = ref Unsafe.As<uint, int>(ref src);

                Vector128<short> a = V128Helper.NarrowSaturate(Vector128.LoadUnsafe(ref data, 00), Vector128.LoadUnsafe(ref data, 04));
                Vector128<short> b = V128Helper.NarrowSaturate(Vector128.LoadUnsafe(ref data, 08), Vector128.LoadUnsafe(ref data, 12));
                Vector128<sbyte> m = V128Helper.NarrowSaturate(a << 15, b << 15);
                uint mask = m.ExtractMostSignificantBits();

                int insertShift = 64 - 16;
                part >>= 16;
                part |= (ulong)mask << insertShift;

                src = ref Unsafe.Add(ref src, 16);
                count -= 16;
            }
        }

        while (count >= 8)
        {
            uint od = 0;
            uint ev = 0;

            od |= Unsafe.Add(ref src, 00) << 00;
            ev |= Unsafe.Add(ref src, 01) << 01;
            od |= Unsafe.Add(ref src, 02) << 02;
            ev |= Unsafe.Add(ref src, 03) << 03;
            od |= Unsafe.Add(ref src, 04) << 04;
            ev |= Unsafe.Add(ref src, 05) << 05;
            od |= Unsafe.Add(ref src, 06) << 06;
            ev |= Unsafe.Add(ref src, 07) << 07;

            int insertShift = 64 - 8;
            part >>= 8;
            part |= (ulong)(od | ev) << insertShift;

            src = ref Unsafe.Add(ref src, 8);
            count -= 8;
        }

        rem = count;
        return part;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Pack2<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        PackCore(destination, start, source, 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Pack3<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        PackCore(destination, start, source, 3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Pack4<P, E>(Span<P> destination, nint start, ReadOnlySpan<E> source)
        where P : unmanaged, IBinaryInteger<P>
        where E : unmanaged, IBinaryInteger<E>
    {
        PackCore(destination, start, source, 4);
    }
}
