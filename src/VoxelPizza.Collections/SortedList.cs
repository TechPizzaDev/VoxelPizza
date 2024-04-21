using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections;

[DebuggerDisplay("Count = {Count}")]
public class SortedList<T> : IList<T>, IReadOnlyList<T>
{
    private T[] _items;
    private int _count;
    private int version;

    private const int DefaultCapacity = 4;

    public SortedList()
    {
        _items = Array.Empty<T>();
        _count = 0;
    }

    public SortedList(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _items = new T[capacity];
    }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The index of the item within the list.</param>
    /// <returns>The item at to the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The specified index is out of range.</exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLess(index);
            }
            return _items[index];
        }
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Adds <paramref name="item"/> to the list.
    /// </summary>
    /// <returns><see langword="true"/> if the item didn't exist; <see langword="false"/> otherwise.</returns>
    public bool Add(T item)
    {
        int i = Array.BinarySearch(_items, 0, _count, item);
        int index = i < 0 ? ~i : i;
        Insert(index, item);
        return i >= 0;
    }

    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Attempts to add <paramref name="item"/> to the list if it isn't already.
    /// </summary>
    /// <returns><see langword="true"/> if the item was added; <see langword="false"/> if the item already existed.</returns>
    public bool TryAdd(T item)
    {
        int i = Array.BinarySearch(_items, 0, _count, item);
        if (i < 0)
        {
            Insert(~i, item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if <paramref name="item"/> exists in the list.
    /// </summary>
    /// <returns><see langword="true"/> if the item is found; <see langword="false"/> otherwise.</returns>
    public bool Contains(T item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes <paramref name="item"/> from the list.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the item is found and removed; <see langword="false"/> otherwise.
    /// </returns>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }
        return false;
    }

    public int Capacity
    {
        get => _items.Length;
        set
        {
            if (value == _items.Length)
            {
                return;
            }
            if (value < _count)
            {
                ThrowHelper.ThrowArgumentOutOfRange_SmallCapacity(value);
            }

            if (value > 0)
            {
                T[] newKeys = new T[value];
                if (_count > 0)
                {
                    Array.Copy(_items, newKeys, _count);
                }
                _items = newKeys;
            }
            else
            {
                _items = Array.Empty<T>();
            }
        }
    }

    /// <summary>
    /// Returns the number of items in the list.
    /// </summary>
    public int Count => _count;

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Removes all items from the list.
    /// </summary>
    public void Clear()
    {
        // clear does not change the capacity
        version++;
        // Don't need to doc this but we clear the elements so that the gc can reclaim the references.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_items, 0, _count);
        }
        _count = 0;
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        CopyTo(array.AsSpan(arrayIndex));
    }

    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < Count)
        {
            ThrowHelper.ThrowArgumentException_DstTooSmall();
        }

        _items.AsSpan(0, destination.Length).CopyTo(destination);
    }

    /// <summary>
    /// Ensures that the capacity of the list is at least the given minimum value.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        int newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;

        // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
        // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
        if ((uint)newCapacity > Array.MaxLength)
            newCapacity = Array.MaxLength;

        if (newCapacity < capacity)
            newCapacity = capacity;

        Capacity = newCapacity;
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return Count == 0 ? Enumerable.Empty<T>().GetEnumerator() : GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    /// <summary>
    /// Finds the index of <paramref name="item"/> in the list.
    /// </summary>
    /// <returns>
    /// The index of the item if found;
    /// otherwise, the bitwise complement of the index of the next element.
    /// If at the end, the bitwise complement of <see cref="Count"/>.
    /// </returns>
    public int BinarySearch(T item)
    {
        int ret = Array.BinarySearch(_items, 0, _count, item);
        return ret;
    }

    /// <summary>
    /// Finds the index of <paramref name="item"/> in the list.
    /// </summary>
    /// <remarks>
    /// The item is located through a binary search, and thus the average execution
    /// time of this method is proportional to Log2(size), where
    /// size is the size of the list.
    /// </remarks>
    /// <returns>
    /// The index of the item, or -1 if the item is not found.
    /// </returns>
    public int IndexOf(T item)
    {
        int ret = BinarySearch(item);
        return ret >= 0 ? ret : -1;
    }

    /// <summary>
    /// Inserts <paramref name="item"/> at a given index.
    /// </summary>
    private void Insert(int index, T item)
    {
        if (_count == _items.Length)
        {
            EnsureCapacity(_count + 1);
        }
        if (index < _count)
        {
            Array.Copy(_items, index, _items, index + 1, _count - index);
        }
        _items[index] = item;
        _count++;
        version++;
    }

    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    /// <summary>
    /// Checks if <paramref name="equalItem"/> exists in the list.
    /// </summary>
    /// <returns><see langword="true"/> if the item is found; <see langword="false"/> otherwise.</returns>
    public bool TryGetValue(T equalItem, [MaybeNullWhen(false)] out T actualItem)
    {
        int i = IndexOf(equalItem);
        if (i >= 0)
        {
            actualItem = _items[i];
            return true;
        }

        actualItem = default;
        return false;
    }

    /// <summary>
    /// Removes the item at the given index. 
    /// </summary>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            ThrowHelper.ThrowArgumentOutOfRange_IndexMustBeLess(index);
        }

        _count--;
        if (index < _count)
        {
            Array.Copy(_items, index + 1, _items, index, _count - index);
        }
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _items[_count] = default!;
        }
        version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemoveLast([MaybeNullWhen(false)] out T item)
    {
        T[] array = _items;
        int index = _count - 1;
        if ((uint)index < (uint)array.Length)
        {
            version++;
            _count = index;
            item = array[index];
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                array[index] = default!;
            }
            return true;
        }
        item = default;
        return false;
    }

    /// <summary>
    /// Sets the capacity of the list to the size of the list.
    /// </summary>
    /// <remarks>
    /// This method can be used to minimize the memory overhead
    /// once it is known that no new elements will be added.
    /// </remarks>
    /// <example>
    /// To allocate minimum size storage array, execute the following statements:
    /// <code>
    /// dictionary.Clear();
    /// dictionary.TrimExcess();
    /// </code>
    /// </example>
    public void TrimExcess()
    {
        int threshold = (int)(_items.Length * 0.9);
        if (_count < threshold)
        {
            Capacity = _count;
        }
    }

    public struct Enumerator : IEnumerator<T>, IEnumerator
    {
        private readonly SortedList<T> _set;
        private readonly int _version;
        private int _index;
        private T? _current;

        internal Enumerator(SortedList<T> set)
        {
            _set = set;
            _version = set.version;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_version != _set.version)
            {
                ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
            }

            if ((uint)_index < (uint)_set.Count)
            {
                _current = _set._items[_index];
                _index++;
                return true;
            }

            _index = _set.Count + 1;
            _current = default;
            return false;
        }

        public readonly T Current => _current!;

        readonly object? IEnumerator.Current => _current;

        public void Reset()
        {
            if (_version != _set.version)
            {
                ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
            }
            _index = 0;
            _current = default;
        }
    }
}