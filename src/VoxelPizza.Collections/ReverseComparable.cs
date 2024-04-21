using System;
using System.Collections.Generic;

namespace VoxelPizza.Collections;

public struct ReverseComparable<T>(T value) : IEquatable<ReverseComparable<T>>, IComparable<ReverseComparable<T>>
{
    public T Value = value;

    public readonly int CompareTo(ReverseComparable<T> other) => Comparer<T>.Default.Compare(other.Value, Value);

    public readonly bool Equals(ReverseComparable<T> other) => EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override readonly bool Equals(object? obj)
    {
        if (obj is ReverseComparable<T> other)
        {
            return Equals(other);
        }
        else if (obj is T otherT)
        {
            return Equals(new ReverseComparable<T>(otherT));
        }
        return false;
    }

    public override readonly int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override readonly string? ToString() => Value?.ToString();

    public static bool operator ==(ReverseComparable<T> left, ReverseComparable<T> right) => left.Equals(right);

    public static bool operator !=(ReverseComparable<T> left, ReverseComparable<T> right) => !(left == right);

    public static bool operator <(ReverseComparable<T> left, ReverseComparable<T> right) => left.CompareTo(right) < 0;

    public static bool operator <=(ReverseComparable<T> left, ReverseComparable<T> right) => left.CompareTo(right) <= 0;

    public static bool operator >(ReverseComparable<T> left, ReverseComparable<T> right) => left.CompareTo(right) > 0;

    public static bool operator >=(ReverseComparable<T> left, ReverseComparable<T> right) => left.CompareTo(right) >= 0;
}
