using System;
using System.Diagnostics;
using System.Numerics;

namespace VoxelPizza.Collections.Bits;

public readonly record struct BitArraySlot(int Part, int Element, ushort BitsPerElement);

public readonly struct BitArray<P, E>
    where P : unmanaged, IBinaryInteger<P>
    where E : unmanaged, IBinaryInteger<E>
{
    private readonly P[] _store;
    private readonly E _elementMask;
    private readonly ushort _elementsPerPart;
    private readonly ushort _bitsPerElement;

    public P[] Store => _store;
    public E ElementMask => _elementMask;
    public int ElementsPerPart => _elementsPerPart;
    public int BitsPerElement => _bitsPerElement;

    public nint Length => (nint)_store.LongLength * _elementsPerPart;

    public E this[int index]
    {
        get => Get(GetSlot(index));
        set => Set(GetSlot(index), value);
    }

    public E this[nint index]
    {
        get => Get(GetSlot(index));
        set => Set(GetSlot(index), value);
    }

    public unsafe BitArray(P[] store, int bitsPerElement)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitsPerElement, sizeof(E) * 8u);

        _store = store;
        _elementMask = BitHelper.GetElementMask<E>(bitsPerElement);
        _elementsPerPart = (ushort)BitHelper.GetElementsPerPart<P>(bitsPerElement);
        _bitsPerElement = (ushort)bitsPerElement;
    }

    public static BitArray<P, E> Allocate(
        nuint count, int bitsPerElement, bool pinned = false)
    {
        nuint partCount = BitHelper.GetPartCount<P>(count, bitsPerElement);
        P[] array = GC.AllocateArray<P>(checked((int)partCount), pinned);
        return new BitArray<P, E>(array, bitsPerElement);
    }

    public static BitArray<P, E> AllocateUninitialized(
        nuint count, int bitsPerElement, bool pinned = false)
    {
        nuint partCount = BitHelper.GetPartCount<P>(count, bitsPerElement);
        P[] array = GC.AllocateUninitializedArray<P>(checked((int)partCount), pinned);
        return new BitArray<P, E>(array, bitsPerElement);
    }

    public BitArraySlot GetSlot(int index)
    {
        (int part, int element) = Math.DivRem(index, _elementsPerPart);
        return new BitArraySlot(part, element * _bitsPerElement, _bitsPerElement);
    }

    public BitArraySlot GetSlot(nint index)
    {
        (nint part, nint element) = Math.DivRem(index, _elementsPerPart);
        return new BitArraySlot((int)part, (int)element * _bitsPerElement, _bitsPerElement);
    }

    public E Get(BitArraySlot slot)
    {
        Debug.Assert(slot.BitsPerElement == _bitsPerElement);

        return BitHelper.Get((ReadOnlySpan<P>)Store, slot.Part, slot.Element, _elementMask);
    }

    public void Set(BitArraySlot slot, E value)
    {
        Debug.Assert(slot.BitsPerElement == _bitsPerElement);

        BitHelper.Set((Span<P>)Store, slot.Part, slot.Element, value, _elementMask);
    }

    public void Fill(nint start, nint count, E value)
    {
        BitHelper.Fill((Span<P>)Store, start, count, value, BitsPerElement);
    }

    public void Fill(E value)
    {
        Fill(0, Length, value);
    }

    public void Get(nint start, Span<E> destination)
    {
        BitHelper.Unpack(destination, (ReadOnlySpan<P>)Store, start, BitsPerElement);
    }

    public void Set(nint start, ReadOnlySpan<E> source)
    {
        BitHelper.Pack((Span<P>)Store, start, source, BitsPerElement);
    }
}
