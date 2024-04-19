using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public abstract partial class BlockStorage
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Expand8To32(ref readonly byte src, ref uint dst, nuint len)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            nuint i = 0;
            for (; i + (nuint)Vector128<byte>.Count <= len; i += (nuint)Vector128<byte>.Count)
            {
                Vector128<byte> v1_8 = Vector128.LoadUnsafe(in src);

                (Vector128<ushort> v1_16, Vector128<ushort> v2_16) = Vector128.Widen(v1_8);

                (Vector128<uint> v1_32, Vector128<uint> v2_32) = Vector128.Widen(v1_16);
                (Vector128<uint> v3_32, Vector128<uint> v4_32) = Vector128.Widen(v2_16);

                v1_32.StoreUnsafe(ref dst, (nuint)(0 * Vector128<uint>.Count));
                v2_32.StoreUnsafe(ref dst, (nuint)(1 * Vector128<uint>.Count));
                v3_32.StoreUnsafe(ref dst, (nuint)(2 * Vector128<uint>.Count));
                v4_32.StoreUnsafe(ref dst, (nuint)(3 * Vector128<uint>.Count));

                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Vector128<byte>.Count);
                dst = ref Unsafe.Add(ref dst, Vector128<byte>.Count);
            }
            len -= i;
        }

        (nuint loops, nuint rem) = Math.DivRem(len, 2);
        for (nuint i = 0; i < loops; i++)
        {
            ushort s = Unsafe.ReadUnaligned<ushort>(in src);
            ulong d = (s & 0xFFu) | ((s & 0xFF00uL) << 24);
            Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref dst), d);

            src = ref Unsafe.Add(ref Unsafe.AsRef(in src), 2 * sizeof(byte));
            dst = ref Unsafe.Add(ref dst, 2);
        }

        if (rem != 0)
        {
            dst = src;
        }
    }

    public static void Expand8To32(ReadOnlySpan<byte> source, Span<uint> destination)
    {
        if (source.Length > destination.Length)
        {
            ThrowDstTooSmall();
        }

        ref byte byteSrc = ref MemoryMarshal.GetReference(source);
        ref uint uintDst = ref MemoryMarshal.GetReference(destination);
        Expand8To32(ref byteSrc, ref uintDst, (nuint)source.Length);
    }

    public static void Expand16To32(ref readonly byte src, ref uint dst, nuint len)
    {
        (nuint loops, nuint rem) = Math.DivRem(len, 2);
        for (nuint i = 0; i < loops; i++)
        {
            uint s = Unsafe.ReadUnaligned<uint>(in src);
            ulong d = (s & 0xFFFFu) | ((s & 0xFFFF0000uL) << 16);
            Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref dst), d);

            src = ref Unsafe.Add(ref Unsafe.AsRef(in src), 2 * sizeof(ushort));
            dst = ref Unsafe.Add(ref dst, 2);
        }

        if (rem != 0)
        {
            dst = Unsafe.ReadUnaligned<ushort>(in src);
        }
    }

    public static void Expand16To32(ReadOnlySpan<ushort> source, ReadOnlySpan<uint> destination)
    {
        if (source.Length > destination.Length)
        {
            ThrowDstTooSmall();
        }

        ref byte byteSrc = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(source));
        ref uint uintDst = ref MemoryMarshal.GetReference(destination);
        Expand16To32(ref byteSrc, ref uintDst, (nuint)source.Length);
    }

    public static void Expand24To32(ref readonly byte src, ref uint dst, nuint len)
    {
        (nuint loops, nuint rem) = Math.DivRem(len, 4);
        for (nuint i = 0; i < loops; i++)
        {
            ulong s01 = Unsafe.ReadUnaligned<ulong>(in src);
            uint s12 = Unsafe.ReadUnaligned<uint>(in Unsafe.Add(ref Unsafe.AsRef(in src), sizeof(ulong)));

            ulong d0 = (s01 & 0x00ffffff) | (((s01 >> 24) & 0x00ffffff) << 32);
            ulong d1 = (s01 >> 48) | ((s12 & 0xff) << 16) | ((ulong)(s12 >> 8) << 32);

            Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref dst, 0)), d0);
            Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref dst, 2)), d1);

            src = ref Unsafe.Add(ref Unsafe.AsRef(in src), 4 * (nuint)Unsafe.SizeOf<UInt24>());
            dst = ref Unsafe.Add(ref dst, 4);
        }

        for (nuint i = 0; i < rem; i++)
        {
            UInt24 d = Unsafe.ReadUnaligned<UInt24>(in src);
            dst = d;

            src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Unsafe.SizeOf<UInt24>());
            dst = ref Unsafe.Add(ref dst, 1);
        }
    }

    public static void Expand24To32(ReadOnlySpan<UInt24> source, ReadOnlySpan<uint> destination)
    {
        if (source.Length > destination.Length)
        {
            ThrowDstTooSmall();
        }

        ref byte byteSrc = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(source));
        ref uint uintDst = ref MemoryMarshal.GetReference(destination);
        Expand24To32(ref byteSrc, ref uintDst, (nuint)source.Length);
    }
}
