using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections;

public class IndexMap<T> : IReadOnlyList<T>, IReadOnlyDictionary<T, int>
    where T : notnull, IEquatable<T>
{
    private readonly BucketDict<T, int> _map;
    private readonly List<T> _list;

    public IndexMap(int capacity)
    {
        _map = new BucketDict<T, int>(capacity);
        _list = new List<T>(capacity);
    }

    public IndexMap(ReadOnlySpan<T> keys) : this(Math.Min(keys.Length, 4))
    {
        while (keys.Length > 0)
        {
            T key = keys[0];

            int len = keys.IndexOfAnyExcept(key);
            if (len == -1)
                len = keys.Length;

            TryAdd(key, out _);

            keys = keys.Slice(len);
        }
    }

    public int this[T key] => _map[key];

    public T this[int index] => _list[index];

    public int Count => _map.Count;

    public BucketDict<T, int>.KeyCollection Keys => _map.Keys;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IEnumerable<T> IReadOnlyDictionary<T, int>.Keys => Keys;

    public BucketDict<T, int>.ValueCollection Values => _map.Values;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IEnumerable<int> IReadOnlyDictionary<T, int>.Values => Values;

    private int GetNextIndex()
    {
        return _map.Count - 1;
    }

    public int IndexOf(T key) => _map[key];

    public bool TryGetValue(T key, out int index) => _map.TryGetValue(key, out index);

    public bool TryAdd(T key, out int index)
    {
        ref int slot = ref _map.GetValueRefOrAddDefault(key, out bool exists);
        if (!exists)
        {
            slot = GetNextIndex();
            _list.Insert(slot, key);
        }
        index = slot;
        return !exists;
    }

    public bool Remove(T value, out int index)
    {
        if (_map.Remove(value, out index))
        {
            // TODO: SWAP Swpapaawp
            _list.RemoveAt(index);
            return true;
        }
        return false;
    }

    public bool ContainsKey(T value) => _map.ContainsKey(value);

    public ReadOnlySpan<T> AsSpan() => CollectionsMarshal.AsSpan(_list);

    public void Clear() => _map.Clear();

    public BucketDict<T, int>.Enumerator GetEnumerator() => _map.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_list).GetEnumerator();

    IEnumerator<KeyValuePair<T, int>> IEnumerable<KeyValuePair<T, int>>.GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<T, int>>)_map).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)_list).GetEnumerator();
}
