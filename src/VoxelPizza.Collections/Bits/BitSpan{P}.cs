using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Bits;

public readonly ref struct BitSpan<P>
    where P : unmanaged, IBinaryInteger<P>
{
    private readonly ref P _data;
    private readonly nint _start;
    private readonly nint _length;
    private readonly ushort _bitsPerElement;
    private readonly ushort _elementsPerPart;

    public BitSpan(ref P data, nint start, nint length, ushort bitsPerElement, ushort elementsPerPart)
    {
        _data = ref data;
        _start = start;
        _length = length;
        _bitsPerElement = bitsPerElement;
        _elementsPerPart = elementsPerPart;
    }

    public unsafe BitSpan(ref P data, nint start, nint length, int bitsPerElement)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(P) * 8u, nameof(bitsPerElement));

        _data = ref data;
        _start = start;
        _length = length;
        _bitsPerElement = (ushort)bitsPerElement;
        _elementsPerPart = (ushort)BitHelper.GetElementsPerPart<P>(bitsPerElement);
    }

    public unsafe BitSpan(ref P data, nint start, nint length)
    {
        _data = ref data;
        _start = start;
        _length = length;
        _bitsPerElement = (ushort)(sizeof(P) * 8u);
        _elementsPerPart = 1;
    }

    public int Length => (int)_length;

    public nint LongLength => _length;

    public int BitsPerElement => _bitsPerElement;

    public int ElementsPerPart => _elementsPerPart;

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element within the span.</param>
    /// <returns>The element at to the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The specified index is out of range.</exception>
    /// <exception cref="OverflowException"></exception>
    public P this[nint index]
    {
        get => Get<P>(index);
        set => Set(index, value);
    }
    
    public void CopyTo<E>(BitSpan<E> span)
        where E : unmanaged, IBinaryInteger<E>
    {
        BitHelper.Convert(this, span);
    }

    public ref P GetReference() => ref _data;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element within the span.</param>
    /// <returns>The element at to the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The specified index is out of range.</exception>
    /// <exception cref="OverflowException"></exception>
    public E Get<E>(nint index, E elementMask)
        where E : unmanaged, IBinaryInteger<E>
    {
        if ((nuint)checked(_start + index) >= (nuint)_length)
            ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLess(index);

        (nint partIndex, nint elementIndex) = Math.DivRem(index, _elementsPerPart);
        int elementOffset = (int)elementIndex * _bitsPerElement;

        P part = Unsafe.Add(ref _data, partIndex);
        E element = E.CreateTruncating(part >> elementOffset) & elementMask;
        return element;
    }

    /// <inheritdoc cref="Get{E}(nint, E)"/>
    public E Get<E>(nint index)
        where E : unmanaged, IBinaryInteger<E>
    {
        E mask = BitHelper.GetElementMask<E>(_bitsPerElement);
        return Get(index, mask);
    }

    /// <summary>
    /// Sets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element within the span.</param>
    /// <exception cref="ArgumentOutOfRangeException">The specified index is out of range.</exception>
    /// <exception cref="OverflowException"></exception>
    public void Set<E>(nint index, E value, E elementMask)
        where E : unmanaged, IBinaryInteger<E>
    {
        if ((nuint)checked(_start + index) >= (nuint)_length)
            ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLess(index);

        (nint partIndex, nint elementIndex) = Math.DivRem(index, _elementsPerPart);
        int elementOffset = (int)elementIndex * _bitsPerElement;

        ref P part = ref Unsafe.Add(ref _data, partIndex);
        P clearMask = P.CreateTruncating(elementMask);
        P setMask = P.CreateTruncating(value & elementMask);
        part &= ~(clearMask << elementOffset);
        part |= setMask << elementOffset;
    }

    /// <inheritdoc cref="Set{E}(nint, E, E)"/>
    public void Set<E>(nint index, E value)
        where E : unmanaged, IBinaryInteger<E>
    {
        E mask = BitHelper.GetElementMask<E>(_bitsPerElement);
        Set(index, value, mask);
    }

    /// <summary>
    /// Forms a slice out of the given span, beginning at 'start'.
    /// </summary>
    /// <param name="start">The index at which to begin this slice.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;Length).
    /// </exception>
    /// <exception cref="OverflowException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BitSpan<P> Slice(nint start)
    {
        checked
        {
            nint newStart = checked(_start + start);

            if ((nuint)newStart > (nuint)_length)
                ThrowHelper.ThrowArgumentOutOfRangeException();

            return new BitSpan<P>(ref _data, newStart, _length - start, _bitsPerElement, _elementsPerPart);
        }
    }
    
    /// <inheritdoc cref="Slice(nint)"/>
    public BitSpan<P> Slice(int start) => Slice((nint)start);

    /// <summary>
    /// Forms a slice out of the given span, beginning at 'start', of given length.
    /// </summary>
    /// <param name="start">The index at which to begin this slice.</param>
    /// <param name="length">The desired length for the slice (exclusive).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;Length).
    /// </exception>
    /// <exception cref="OverflowException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BitSpan<P> Slice(nint start, nint length)
    {
        nint newStart = checked(_start + start);
        nint newLength = _length - start;

        if (newStart < _start || (nuint)length > (nuint)newLength)
            ThrowHelper.ThrowArgumentOutOfRangeException();

        return new BitSpan<P>(ref _data, newStart, newLength, _bitsPerElement, _elementsPerPart);
    }

    /// <inheritdoc cref="Slice(nint, nint)"/>
    public BitSpan<P> Slice(int start, int count) => Slice((nint)start, count);
}
