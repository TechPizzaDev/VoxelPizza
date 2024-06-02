using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections.Bits;

public readonly record struct BitArraySlot(int Part, int Element, ushort BitsPerElement);

public readonly struct BitArray<P>
    where P : unmanaged, IBinaryInteger<P>
{
    private readonly P[] _store;
    private readonly ushort _bitsPerElement;
    private readonly ushort _elementsPerPart;

    public P[] Store => _store;
    public int BitsPerElement => _bitsPerElement;
    public int ElementsPerPart => _elementsPerPart;

    public nint Length => (nint)_store.LongLength * _elementsPerPart;

    public unsafe BitArray(P[] store, int bitsPerElement)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bitsPerElement, sizeof(P) * 8u);

        _store = store;
        _bitsPerElement = (ushort)bitsPerElement;
        _elementsPerPart = (ushort)BitHelper.GetElementsPerPart<P>(bitsPerElement);
    }

    public BitArray()
    {
        _store = Array.Empty<P>();
        _bitsPerElement = 0;
        _elementsPerPart = 1;
    }

    public static BitArray<P> Allocate(
        nuint count, int bitsPerElement, bool pinned = false)
    {
        if (bitsPerElement == 0)
        {
            return new BitArray<P>();
        }

        nuint partCount = BitHelper.GetPartCount<P>(count, bitsPerElement);
        P[] array = GC.AllocateArray<P>(checked((int)partCount), pinned);
        return new BitArray<P>(array, bitsPerElement);
    }

    public static BitArray<P> AllocateUninitialized(
        nuint count, int bitsPerElement, bool pinned = false)
    {
        if (bitsPerElement == 0)
        {
            return new BitArray<P>();
        }

        nuint partCount = BitHelper.GetPartCount<P>(count, bitsPerElement);
        P[] array = GC.AllocateUninitializedArray<P>(checked((int)partCount), pinned);
        return new BitArray<P>(array, bitsPerElement);
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

    public E Get<E>(BitArraySlot slot)
        where E : unmanaged, IBinaryInteger<E>
    {
        Debug.Assert(slot.BitsPerElement == _bitsPerElement);

        E mask = BitHelper.GetElementMask<E>(slot.BitsPerElement);
        return BitHelper.Get((ReadOnlySpan<P>)Store, slot.Part, slot.Element, mask);
    }

    public void Set<E>(BitArraySlot slot, E value)
        where E : unmanaged, IBinaryInteger<E>
    {
        Debug.Assert(slot.BitsPerElement == _bitsPerElement);

        E mask = BitHelper.GetElementMask<E>(slot.BitsPerElement);
        BitHelper.Set((Span<P>)Store, slot.Part, slot.Element, value, mask);
    }

    public void GetRange<E>(nint start, Span<E> destination)
        where E : unmanaged, IBinaryInteger<E>
    {
        BitHelper.Unpack(destination, (ReadOnlySpan<P>)Store, start, BitsPerElement);
    }

    public nint SetRange<E>(nint start, ReadOnlySpan<E> source, ChangeTracking changeTracking)
        where E : unmanaged, IBinaryInteger<E>
    {
        Span<P> store = Store;
        int bpe = BitsPerElement;

        return changeTracking switch
        {
            ChangeTracking.None => new BitTrackerNone<P>().Pack(store, start, source, bpe).ChangeCount,
            ChangeTracking.Any => new BitTrackerAny<P>().Pack(store, start, source, bpe).ChangeCount,
            ChangeTracking.All => new BitTrackerAll<P>().Pack(store, start, source, bpe).ChangeCount,
            _ => ThrowHelper.ThrowArgumentOutOfRangeException(changeTracking, 0)
        };
    }

    public BitSpan<P> AsBitSpan()
    {
        return new BitSpan<P>(ref MemoryMarshal.GetArrayDataReference(Store), 0, Length, _bitsPerElement, _elementsPerPart);
    }

    public BitSpan<P> AsBitSpan(nint start)
    {
        return AsBitSpan().Slice(start);
    }

    public BitSpan<P> AsBitSpan(nint start, nint count)
    {
        return AsBitSpan().Slice(start, count);
    }
}
