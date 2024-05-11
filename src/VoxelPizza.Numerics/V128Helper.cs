using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Numerics;

public static class V128Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> CreateIncrement<T>([ConstantExpected] T start, [ConstantExpected] T step)
        where T : unmanaged, INumberBase<T>
    {
        Unsafe.SkipInit(out Vector128<T> vec);
        for (int i = 0; i < Vector128<T>.Count; i++)
        {
            vec = vec.WithElement(i, start + step * T.CreateTruncating(i));
        }
        return vec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> CreateIncrementPow2<T>([ConstantExpected] int start, [ConstantExpected] int step)
        where T : unmanaged, INumberBase<T>, IShiftOperators<T, int, T>
    {
        Unsafe.SkipInit(out Vector128<T> vec);
        for (int i = 0; i < Vector128<T>.Count; i++)
        {
            vec = vec.WithElement(i, T.One << (start + i * step));
        }
        return vec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Remainder(Vector128<float> x, Vector128<float> y)
    {
        return x - Truncate(x / y) * y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Truncate(Vector128<float> value)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.RoundToZero(value);
        }
        else if (AdvSimd.IsSupported)
        {
            return AdvSimd.RoundToZero(value);
        }
        else if (PackedSimd.IsSupported)
        {
            return PackedSimd.Truncate(value);
        }
        else
        {
            return SoftwareFallback(value);
        }

        static Vector128<float> SoftwareFallback(Vector128<float> value)
        {
            Unsafe.SkipInit(out Vector128<float> result);
            for (int i = 0; i < Vector128<float>.Count; i++)
            {
                result = result.WithElement(i, MathF.Truncate(value.GetElement(i)));
            }
            return result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> NarrowSaturate(Vector128<int> lower, Vector128<int> upper)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.PackSignedSaturate(lower, upper);
        }
        else if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractNarrowingSaturateUpper(
                AdvSimd.ExtractNarrowingSaturateLower(lower),
                upper);
        }
        else if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertNarrowingSaturateSigned(lower, upper);
        }
        else
        {
            return NarrowSaturateFallback<int, short>(lower, upper);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> NarrowSaturate(Vector128<short> lower, Vector128<short> upper)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.PackSignedSaturate(lower, upper);
        }
        else if (AdvSimd.IsSupported)
        {
            return AdvSimd.ExtractNarrowingSaturateUpper(
                AdvSimd.ExtractNarrowingSaturateLower(lower),
                upper);
        }
        else if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertNarrowingSaturateSigned(lower, upper);
        }
        else
        {
            return NarrowSaturateFallback<short, sbyte>(lower, upper);
        }
    }

    private static Vector128<TTo> NarrowSaturateFallback<TFrom, TTo>(Vector128<TFrom> lower, Vector128<TFrom> upper)
        where TFrom : INumberBase<TFrom>
        where TTo : INumberBase<TTo>
    {
        Unsafe.SkipInit(out Vector128<TTo> result);
        for (int i = 0; i < Vector128<TTo>.Count; i++)
        {
            result = result.WithElement(i + Vector128<TTo>.Count * 0, TTo.CreateSaturating(lower.GetElement(i)));
            result = result.WithElement(i + Vector128<TTo>.Count * 1, TTo.CreateSaturating(upper.GetElement(i)));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool IsAcceleratedShiftRightLogical<T>()
        where T : unmanaged
    {
        if (Avx512BW.VL.IsSupported)
        {
            if (sizeof(T) == sizeof(short))
                return true;
        }

        if (Avx2.IsSupported)
        {
            if (sizeof(T) == sizeof(int) ||
                sizeof(T) == sizeof(long))
                return true;
        }

        return AdvSimd.IsSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector128<T> ShiftRightLogical<T>(Vector128<T> value, Vector128<T> count)
        where T : unmanaged, INumberBase<T>, IShiftOperators<T, int, T>
    {
        if (Avx512BW.VL.IsSupported)
        {
            if (sizeof(T) == sizeof(short))
                return Avx512BW.VL.ShiftRightLogicalVariable(value.AsInt16(), count.AsUInt16()).As<short, T>();
        }

        if (Avx2.IsSupported)
        {
            if (sizeof(T) == sizeof(int))
                return Avx2.ShiftRightLogicalVariable(value.AsInt32(), count.AsUInt32()).As<int, T>();
            if (sizeof(T) == sizeof(long))
                return Avx2.ShiftRightLogicalVariable(value.AsInt64(), count.AsUInt64()).As<long, T>();
        }

        if (AdvSimd.IsSupported)
        {
            return sizeof(T) switch
            {
                sizeof(byte) => AdvSimd.ShiftLogical(value.AsByte(), -count.AsSByte()).As<byte, T>(),
                sizeof(short) => AdvSimd.ShiftLogical(value.AsInt16(), -count.AsInt16()).As<short, T>(),
                sizeof(int) => AdvSimd.ShiftLogical(value.AsInt32(), -count.AsInt32()).As<int, T>(),
                sizeof(long) => AdvSimd.ShiftLogical(value.AsInt64(), -count.AsInt64()).As<long, T>(),
                _ => ShiftRightLogicalFallback(value, count),
            };
        }

        return ShiftRightLogicalFallback(value, count);
    }

    private static Vector128<T> ShiftRightLogicalFallback<T>(Vector128<T> value, Vector128<T> count)
        where T : INumberBase<T>, IShiftOperators<T, int, T>
    {
        Unsafe.SkipInit(out Vector128<T> result);
        for (int i = 0; i < Vector128<T>.Count; i++)
        {
            result = result.WithElement(i, value.GetElement(i) >>> int.CreateTruncating(count.GetElement(i)));
        }
        return result;
    }
}
