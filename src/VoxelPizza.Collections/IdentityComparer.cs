// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace VoxelPizza.Collections;

public readonly struct IdentityComparer<T> : IComparer<T>, IEqualityComparer<T>, IEquatable<IdentityComparer<T>>
{
    public int Compare(T? x, T? y) => Comparer<T>.Default.Compare(x, y);

    public bool Equals(T? x, T? y) => EqualityComparer<T>.Default.Equals(x, y);

    public bool Equals(IdentityComparer<T> other) => true;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is IdentityComparer<T>;

    public int GetHashCode([DisallowNull] T obj) => EqualityComparer<T>.Default.GetHashCode(obj);
}
