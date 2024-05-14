using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    private static bool IsFastBmi2 { get; } = CheckFastBmi2();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool UseBmi2X64<E>() where E : unmanaged
    {
        // 64-bit can fit at most 2 elements at sizeof(E) greater than 3, so barely worth it to use BMI2.
        return sizeof(E) <= 3 && IsFastBmi2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool UseBmi2<E>() where E : unmanaged
    {
        // 32-bit can fit at most 1 elements at sizeof(E) greater than 2, so not worth it to use BMI2.
        return sizeof(E) <= 2 && IsFastBmi2;
    }

    private static bool CheckFastBmi2()
    {
        if (!X86Base.IsSupported)
        {
            return false;
        }

        (int eax0, int ebx0, int ecx0, int edx0) = X86Base.CpuId(0, 0);
        if (eax0 <= 0)
        {
            // Can't check vendor, so assume fast.
            return true;
        }

        Span<int> vendorString = [ebx0, edx0, ecx0];
        if (!MemoryMarshal.AsBytes(vendorString).SequenceEqual("AuthenticAMD"u8))
        {
            // Vendor is not AMD, and all Intel CPUs should have fast BMI2.
            return true;
        }

        (int eax1, _, _, _) = X86Base.CpuId(1, 0);
        int family = ((eax1 & 0x0FF00000) >> 20) + ((eax1 & 0x0F00) >> 8);
        
        // Zen 2 and below emulated BMI2 poorly in microcode.
        const int zen3Family = 0x19;
        return family >= zen3Family;
    }
}
