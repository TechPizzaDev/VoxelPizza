using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Numerics;

public static class V256Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> NarrowSaturate(Vector256<int> lower, Vector256<int> upper)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.PackSignedSaturate(lower, upper);
        }
        else
        {
            return Vector256.Create(
                V128Helper.NarrowSaturate(lower.GetLower(), upper.GetLower()),
                V128Helper.NarrowSaturate(lower.GetUpper(), upper.GetUpper()));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<sbyte> NarrowSaturate(Vector256<short> lower, Vector256<short> upper)
    {
        if (Avx2.IsSupported)
        {
            return Avx2.PackSignedSaturate(lower, upper);
        }
        else
        {
            return Vector256.Create(
                V128Helper.NarrowSaturate(lower.GetLower(), upper.GetLower()),
                V128Helper.NarrowSaturate(lower.GetUpper(), upper.GetUpper()));
        }
    }
}
