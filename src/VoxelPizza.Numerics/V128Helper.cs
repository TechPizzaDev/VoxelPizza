using System;
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
}
