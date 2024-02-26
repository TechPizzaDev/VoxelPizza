using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Numerics;

internal static class V128Helper
{
    public static Vector128<float> Remainder(Vector128<float> x, Vector128<float> y)
    {
        return x - Truncate(x / y) * y;
    }

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
}
