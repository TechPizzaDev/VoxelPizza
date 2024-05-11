using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace VoxelPizza.Numerics;

public static class V64Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector64<T> CreateIncrement<T>([ConstantExpected] T start, [ConstantExpected] T step)
        where T : unmanaged, INumberBase<T>
    {
        Unsafe.SkipInit(out Vector64<T> vec);
        for (int i = 0; i < Vector64<T>.Count; i++)
        {
            vec = vec.WithElement(i, start + step * T.CreateTruncating(i));
        }
        return vec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool IsAcceleratedShiftRightLogical<T>()
        where T : unmanaged
    {
        return AdvSimd.IsSupported && sizeof(T) != sizeof(long);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector64<T> ShiftRightLogical<T>(Vector64<T> value, Vector64<T> count)
        where T : unmanaged, INumberBase<T>, IShiftOperators<T, int, T>
    {
        if (AdvSimd.IsSupported)
        {
            return sizeof(T) switch
            {
                sizeof(byte) => AdvSimd.ShiftLogical(value.AsByte(), -count.AsSByte()).As<byte, T>(),
                sizeof(short) => AdvSimd.ShiftLogical(value.AsInt16(), -count.AsInt16()).As<short, T>(),
                sizeof(int) => AdvSimd.ShiftLogical(value.AsInt32(), -count.AsInt32()).As<int, T>(),
                _ => ShiftRightLogicalFallback(value, count),
            };
        }

        return ShiftRightLogicalFallback(value, count);
    }

    private static Vector64<T> ShiftRightLogicalFallback<T>(Vector64<T> value, Vector64<T> count)
        where T : INumberBase<T>, IShiftOperators<T, int, T>
    {
        Unsafe.SkipInit(out Vector64<T> result);
        for (int i = 0; i < Vector64<T>.Count; i++)
        {
            result = result.WithElement(i, value.GetElement(i) >>> int.CreateTruncating(count.GetElement(i)));
        }
        return result;
    }
}
