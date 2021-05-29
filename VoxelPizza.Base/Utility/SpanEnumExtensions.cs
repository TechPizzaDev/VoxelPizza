using System;
using System.Runtime.InteropServices;

namespace VoxelPizza
{
    public static class SpanEnumExtensions
    {
        public static bool SequenceEqual<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> other)
            where T : unmanaged, Enum
        {
            return MemoryMarshal.AsBytes(first).SequenceEqual(MemoryMarshal.AsBytes(other));
        }

        public static bool SequenceEqual<T>(this Span<T> first, Span<T> other)
            where T : unmanaged, Enum
        {
            return SequenceEqual((ReadOnlySpan<T>)first, other);
        }
    }
}
