using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Numerics;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct UInt24 : IEquatable<UInt24>
{
    private readonly byte _e0;
    private readonly byte _e1;
    private readonly byte _e2;

    public UInt24(byte e0, byte e1, byte e2)
    {
        _e0 = e0;
        _e1 = e1;
        _e2 = e2;
    }

    public static implicit operator uint(UInt24 value)
    {
        return value._e0 | ((uint)value._e1 << 8) | ((uint)value._e2 << 16);
    }

    public static explicit operator UInt24(uint value)
    {
        return new UInt24((byte)value, (byte)(value >> 8), (byte)(value >> 16));
    }

    public bool Equals(UInt24 other)
    {
        return (uint)this == (uint)other;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is UInt24 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ((uint)this).GetHashCode();
    }

    public override string ToString()
    {
        return ((uint)this).ToString();
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}
