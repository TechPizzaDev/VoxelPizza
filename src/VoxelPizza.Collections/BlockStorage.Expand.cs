using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections;

public abstract partial class BlockStorage
{
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
