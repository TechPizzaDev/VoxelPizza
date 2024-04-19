using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks;

public abstract partial class BlockStorage
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Narrow(ref readonly uint src, ref byte dst, nuint length)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            while (length >= (nuint)Vector128<byte>.Count)
            {
                Vector128<uint> loA = Vector128.LoadUnsafe(in src, (nuint)(0 * Vector128<uint>.Count));
                Vector128<uint> hiA = Vector128.LoadUnsafe(in src, (nuint)(1 * Vector128<uint>.Count));
                Vector128<uint> loB = Vector128.LoadUnsafe(in src, (nuint)(2 * Vector128<uint>.Count));
                Vector128<uint> hiB = Vector128.LoadUnsafe(in src, (nuint)(3 * Vector128<uint>.Count));
                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Vector128<byte>.Count);

                Vector128<short> lo = V128Helper.NarrowSaturate(loA.AsInt32(), hiA.AsInt32());
                Vector128<short> hi = V128Helper.NarrowSaturate(loB.AsInt32(), hiB.AsInt32());
                Vector128<byte> value = V128Helper.NarrowSaturate(lo, hi).AsByte();

                value.StoreUnsafe(ref dst);
                dst = ref Unsafe.Add(ref dst, Vector128<byte>.Count);

                length -= (nuint)Vector128<byte>.Count;
            }
        }

        for (nuint i = 0; i < length; i++)
        {
            uint value = Unsafe.Add(ref Unsafe.AsRef(in src), i);
            Unsafe.Add(ref dst, i) = (byte)value;
        }
    }

    public static void Narrow(ReadOnlySpan<uint> source, Span<byte> destination)
    {
        if (source.Length > destination.Length)
        {
            ThrowDstTooSmall();
        }

        ref uint srcU32 = ref MemoryMarshal.GetReference(source);
        ref byte dstU8 = ref MemoryMarshal.GetReference(destination);
        Narrow(ref srcU32, ref dstU8, (nuint)source.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Narrow(ref readonly uint src, ref ushort dst, nuint length)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            while (length >= (nuint)Vector128<ushort>.Count)
            {
                Vector128<uint> lo = Vector128.LoadUnsafe(in src);
                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Vector128<uint>.Count);

                Vector128<uint> hi = Vector128.LoadUnsafe(in src);
                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Vector128<uint>.Count);

                Vector128<ushort> value = V128Helper.NarrowSaturate(lo.AsInt32(), hi.AsInt32()).AsUInt16();

                value.StoreUnsafe(ref dst);
                dst = ref Unsafe.Add(ref dst, Vector128<ushort>.Count);

                length -= (nuint)Vector128<ushort>.Count;
            }
        }

        for (nuint i = 0; i < length; i++)
        {
            uint value = Unsafe.Add(ref Unsafe.AsRef(in src), i);
            Unsafe.Add(ref dst, i) = (ushort)value;
        }
    }

    public static void Narrow(ReadOnlySpan<uint> source, Span<ushort> destination)
    {
        if (source.Length > destination.Length)
        {
            ThrowDstTooSmall();
        }

        ref uint srcU32 = ref MemoryMarshal.GetReference(source);
        ref ushort dstU16 = ref MemoryMarshal.GetReference(destination);
        Narrow(ref srcU32, ref dstU16, (nuint)source.Length);
    }
}
