// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException()
    {
        throw new ArgumentOutOfRangeException();
    }
    
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRangeException<T>(
        T paramValue, 
        [CallerArgumentExpression(nameof(paramValue))] string? paramName = null)
    {
        throw new ArgumentOutOfRangeException(paramName, paramValue, null);
    }

    [DoesNotReturn]
    public static R ThrowArgumentOutOfRangeException<T, R>(
        T paramValue, 
        R returnValue, 
        [CallerArgumentExpression(nameof(paramValue))] string? paramName = null)
    {
        throw new ArgumentOutOfRangeException(paramName, paramValue, null);
    }

    [DoesNotReturn]
    public static void ThrowKeyNotFoundException<T>(T key)
    {
        throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
    }

    [DoesNotReturn]
    public static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported()
    {
        throw new InvalidOperationException(
            "Operations that change non-concurrent collections must have exclusive access. " +
            "A concurrent update was performed on this collection and corrupted its state. " +
            "The collection's state is no longer correct.");
    }

    [DoesNotReturn]
    public static void ThrowInvalidOperationException_EnumOpCantHappen()
    {
        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
    }

    [DoesNotReturn]
    public static void ThrowInvalidOperationException_EnumFailedVersion()
    {
        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
    }

    [DoesNotReturn]
    public static void ThrowArgumentException_DstTooSmall()
    {
        throw new ArgumentException(
            "Destination is not long enough to copy all the items in the collection.");
    }
    
    [DoesNotReturn]
    public static void ThrowArgumentException_InvalidOffLen()
    {
        throw new ArgumentException(
            "Offset and length were out of bounds for the array or " +
            "count is greater than the number of elements from index to the end of the source collection.");
    }

    [DoesNotReturn]
    public static void ThrowArgumentException_AddingDuplicateWithKey<T>(
        T key,
        [CallerArgumentExpression(nameof(key))] string? paramName = null)
    {
        throw new ArgumentException(
            $"An item with the same key has already been added. Key: {key}", paramName);
    }
    
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRange_SmallCapacity<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        throw new ArgumentOutOfRangeException(paramName, value, "Capacity is less than the current size.");
    }

    
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRange_IndexMustBeLess<T>(
        T index, 
        [CallerArgumentExpression(nameof(index))] string? paramName = null)
    {
        throw new ArgumentOutOfRangeException(
            paramName, index, "Index was out of range. Must be non-negative and less than the size of the collection.");
    }

    [DoesNotReturn]
    public static void ThrowNotSupportedException_KeyCollectionSet()
    {
        throw new NotSupportedException("Mutating a key collection derived from a dictionary is not allowed.");
    }

    [DoesNotReturn]
    public static void ThrowNotSupportedException_ValueCollectionSet()
    {
        throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");
    }
}
