// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections;

public class BucketDict<TKey, TValue> : BucketDict<TKey, TValue, IdentityComparer<TKey>>
    where TKey : notnull
{
    public BucketDict()
    {
    }

    public BucketDict(int capacity) : base(capacity)
    {
    }
}

[DebuggerDisplay("Count = {Count}")]
public class BucketDict<TKey, TValue, TComparer> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
    where TComparer : IEqualityComparer<TKey>
{
    private int[]? _buckets;
    private Entry[]? _entries;
    private ulong _fastModMultiplier;
    private int _count;
    private int _freeList;
    private int _freeCount;
    private int _version;
    private TComparer? _comparer;

    private const int StartOfFreeList = -3;

    public BucketDict() : this(0, default) { }

    public BucketDict(int capacity) : this(capacity, default) { }

    public BucketDict(TComparer? comparer) : this(0, comparer) { }

    public BucketDict(int capacity, TComparer? comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (capacity > 0)
        {
            Initialize(capacity);
        }

        _comparer = comparer;
    }

    public BucketDict(IDictionary<TKey, TValue> dictionary) : this(dictionary, default) { }

    public BucketDict(IDictionary<TKey, TValue> dictionary, TComparer? comparer) :
        this(dictionary?.Count ?? 0, comparer)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        AddRange(dictionary);
    }

    public BucketDict(IEnumerable<KeyValuePair<TKey, TValue>> collection) : this(collection, default) { }

    public BucketDict(IEnumerable<KeyValuePair<TKey, TValue>> collection, TComparer? comparer) :
        this(collection.TryGetNonEnumeratedCount(out int count) ? count : 0, comparer)
    {
        ArgumentNullException.ThrowIfNull(collection);

        AddRange(collection);
    }

    private void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
    {
        // It is likely that the passed-in enumerable is Dictionary<TKey,TValue>. When this is the case,
        // avoid the enumerator allocation and overhead by looping through the entries array directly.
        // We only do this when dictionary is Dictionary<TKey,TValue> and not a subclass, to maintain
        // back-compat with subclasses that may have overridden the enumerator behavior.
        if (enumerable.GetType() == typeof(BucketDict<TKey, TValue, TComparer>))
        {
            BucketDict<TKey, TValue, TComparer> source = (BucketDict<TKey, TValue, TComparer>)enumerable;

            if (source.Count == 0)
            {
                // Nothing to copy, all done
                return;
            }

            // This is not currently a true .AddRange as it needs to be an initialized dictionary
            // of the correct size, and also an empty dictionary with no current entities (and no argument checks).
            Debug.Assert(source._entries is not null);
            Debug.Assert(_entries is not null);
            Debug.Assert(_entries.Length >= source.Count);
            Debug.Assert(_count == 0);

            Entry[] oldEntries = source._entries;
            if (EqualityComparer<TComparer>.Default.Equals(source._comparer, _comparer))
            {
                // If comparers are the same, we can copy _entries without rehashing.
                CopyEntries(oldEntries, source._count);
                return;
            }

            // Comparers differ need to rehash all the entries via Add
            int count = source._count;
            for (int i = 0; i < count; i++)
            {
                // Only copy if an entry
                if (oldEntries[i].next >= -1)
                {
                    Add(oldEntries[i].key, oldEntries[i].value);
                }
            }
            return;
        }

        // We similarly special-case KVP<>[] and List<KVP<>>, as they're commonly used to seed dictionaries, and
        // we want to avoid the enumerator costs (e.g. allocation) for them as well. Extract a span if possible.
        ReadOnlySpan<KeyValuePair<TKey, TValue>> span;
        if (enumerable is KeyValuePair<TKey, TValue>[] array)
        {
            span = array;
        }
        else if (enumerable.GetType() == typeof(List<KeyValuePair<TKey, TValue>>))
        {
            span = CollectionsMarshal.AsSpan((List<KeyValuePair<TKey, TValue>>)enumerable);
        }
        else
        {
            // Fallback path for all other enumerables
            foreach (KeyValuePair<TKey, TValue> pair in enumerable)
            {
                Add(pair.Key, pair.Value);
            }
            return;
        }

        // We got a span. Add the elements to the dictionary.
        foreach (KeyValuePair<TKey, TValue> pair in span)
        {
            Add(pair.Key, pair.Value);
        }
    }

    public TComparer? Comparer => _comparer;

    public int Count => _count - _freeCount;

    /// <summary>
    /// Gets the total numbers of elements the internal data structure can hold without resizing.
    /// </summary>
    public int Capacity => _entries?.Length ?? 0;

    public KeyCollection Keys => new(this);

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    public ValueCollection Values => new(this);

    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    public TValue this[TKey key]
    {
        get
        {
            ref TValue value = ref FindValue(key);
            if (!Unsafe.IsNullRef(ref value))
            {
                return value;
            }

            ThrowHelper.ThrowKeyNotFoundException(key);
            return default;
        }
        set
        {
            bool modified = TryInsert(key, value, InsertionBehavior.OverwriteExisting);
            Debug.Assert(modified);
        }
    }

    public void Add(TKey key, TValue value)
    {
        bool modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified); // If there was an existing key and the Add failed, an exception will already have been thrown.
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair) =>
        Add(keyValuePair.Key, keyValuePair.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
    {
        ref TValue value = ref FindValue(keyValuePair.Key);
        if (!Unsafe.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value))
        {
            return true;
        }

        return false;
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
    {
        ref TValue value = ref FindValue(keyValuePair.Key);
        if (!Unsafe.IsNullRef(ref value) && EqualityComparer<TValue>.Default.Equals(value, keyValuePair.Value))
        {
            Remove(keyValuePair.Key);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        int count = _count;
        if (count > 0)
        {
            Debug.Assert(_buckets != null, "_buckets should be non-null");
            Debug.Assert(_entries != null, "_entries should be non-null");

            Array.Clear(_buckets);

            _count = 0;
            _freeList = -1;
            _freeCount = 0;
            Array.Clear(_entries, 0, count);
        }
    }

    public bool ContainsKey(TKey key) => !Unsafe.IsNullRef(ref FindValue(key));

    public bool ContainsValue(TValue value)
    {
        Entry[]? entries = _entries;
        if (value == null)
        {
            for (int i = 0; i < _count; i++)
            {
                if (entries![i].next >= -1 && entries[i].value == null)
                {
                    return true;
                }
            }
        }
        else if (typeof(TValue).IsValueType)
        {
            // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
            for (int i = 0; i < _count; i++)
            {
                if (entries![i].next >= -1 && EqualityComparer<TValue>.Default.Equals(entries[i].value, value))
                {
                    return true;
                }
            }
        }
        else
        {
            // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
            // https://github.com/dotnet/runtime/issues/10050
            // So cache in a local rather than get EqualityComparer per loop iteration
            EqualityComparer<TValue> defaultComparer = EqualityComparer<TValue>.Default;
            for (int i = 0; i < _count; i++)
            {
                if (entries![i].next >= -1 && defaultComparer.Equals(entries[i].value, value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) => CopyTo(array.AsSpan(index));

    public void CopyTo(Span<KeyValuePair<TKey, TValue>> destination)
    {
        if (destination.Length < Count)
        {
            ThrowHelper.ThrowArgumentException_DstTooSmall();
        }

        int index = 0;
        int count = _count;
        Entry[]? entries = _entries;
        for (int i = 0; i < count; i++)
        {
            if (entries![i].next >= -1)
            {
                destination[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
            }
        }
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
        Count == 0 ? Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator() :
        GetEnumerator();

    internal ref TValue FindValue(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        ref Entry entry = ref Unsafe.NullRef<Entry>();
        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "expected entries to be != null");
            TComparer? comparer = _comparer;

            uint hashCode = (uint)key.GetHashCode();
            int i = GetBucket(hashCode);
            Entry[]? entries = _entries;
            uint collisionCount = 0;

            // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
            i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.
            do
            {
                // Test in if to drop range check for following array access
                if ((uint)i >= (uint)entries.Length)
                {
                    goto ReturnNotFound;
                }

                entry = ref entries[i];
                if (Equals(entry.key, key, comparer))
                {
                    entry.AssertHash(hashCode);
                    goto ReturnFound;
                }

                i = entry.next;

                collisionCount++;
            }
            while (collisionCount <= (uint)entries.Length);

            // The chain of entries forms a loop; which means a concurrent update has happened.
            // Break out of the loop and throw, rather than looping forever.
            goto ConcurrentOperation;
        }

        goto ReturnNotFound;

        ConcurrentOperation:
        ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
        ReturnFound:
        ref TValue value = ref entry.value;
        Return:
        return ref value;
        ReturnNotFound:
        value = ref Unsafe.NullRef<TValue>();
        goto Return;
    }

    [MemberNotNull(nameof(_buckets), nameof(_entries))]
    private int Initialize(int capacity)
    {
        int size = HashHelpers.GetPrime(capacity);
        int[] buckets = new int[size];
        Entry[] entries = new Entry[size];

        // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
        _freeList = -1;
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);

        _buckets = buckets;
        _entries = entries;

        return size;
    }

    private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
    {
        ref TValue? valueRef = ref GetValueRefOrAddDefault(key, out bool exists);
        if (behavior == InsertionBehavior.OverwriteExisting)
        {
            valueRef = value;
            return true;
        }

        if (exists && behavior == InsertionBehavior.ThrowOnExisting)
        {
            ThrowHelper.ThrowArgumentException_AddingDuplicateWithKey(key);
        }

        return false;
    }

    public ref TValue? GetValueRefOrAddDefault(TKey key, out bool exists)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_buckets == null)
        {
            Initialize(0);
        }
        Debug.Assert(_buckets != null);

        Entry[]? entries = _entries;
        Debug.Assert(entries != null, "expected entries to be non-null");

        TComparer? comparer = _comparer;
        uint hashCode = GetHashCode(key, comparer);

        uint collisionCount = 0;
        ref int bucket = ref GetBucket(hashCode);
        int i = bucket - 1; // Value in _buckets is 1-based

        while ((uint)i < (uint)entries.Length)
        {
            if (Equals(entries[i].key, key, comparer))
            {
                entries[i].AssertHash(hashCode);
                exists = true;

                return ref entries[i].value!;
            }

            i = entries[i].next;

            collisionCount++;
            if (collisionCount > (uint)entries.Length)
            {
                // The chain of entries forms a loop; which means a concurrent update has happened.
                // Break out of the loop and throw, rather than looping forever.
                ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
            }
        }

        int index;
        if (_freeCount > 0)
        {
            index = _freeList;
            Debug.Assert((StartOfFreeList - entries[_freeList].next) >= -1, "shouldn't overflow because `next` cannot underflow");
            _freeList = StartOfFreeList - entries[_freeList].next;
            _freeCount--;
        }
        else
        {
            int count = _count;
            if (count == entries.Length)
            {
                Resize();
                bucket = ref GetBucket(hashCode);
            }
            index = count;
            _count = count + 1;
            entries = _entries;
        }

        ref Entry entry = ref entries![index];
#if DEBUG
        entry.hashCode = hashCode;
#endif
        entry.next = bucket - 1; // Value in _buckets is 1-based
        entry.key = key;
        entry.value = default!;
        bucket = index + 1; // Value in _buckets is 1-based
        _version++;

        // Value types never rehash
        if (!typeof(TKey).IsValueType &&
            collisionCount > HashHelpers.HashCollisionThreshold &&
            /*comparer is NonRandomizedStringEqualityComparer*/ false)
        {
            // If we hit the collision threshold we'll need to switch to the comparer which is using randomized string hashing
            // i.e. EqualityComparer<string>.Default.
            Resize(entries.Length, true);
        }

        exists = false;

        return ref entry.value!;
    }

    private void Resize() => Resize(HashHelpers.ExpandPrime(_count), false);

    private void Resize(int newSize, bool forceNewHashCodes)
    {
        // Value types never rehash
        Debug.Assert(!forceNewHashCodes || !typeof(TKey).IsValueType);
        Debug.Assert(_entries != null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        Entry[] entries = new Entry[newSize];

        int count = _count;
        Array.Copy(_entries, entries, count);

        //if (!typeof(TKey).IsValueType && forceNewHashCodes)
        //{
        //    Debug.Assert(_comparer is NonRandomizedStringEqualityComparer);
        //    IEqualityComparer<TKey> comparer = _comparer = (IEqualityComparer<TKey>)((NonRandomizedStringEqualityComparer)_comparer).GetRandomizedEqualityComparer();
        //
        //    for (int i = 0; i < count; i++)
        //    {
        //        if (entries[i].next >= -1)
        //        {
        //            entries[i].hashCode = (uint)comparer.GetHashCode(entries[i].key);
        //        }
        //    }
        //}

        // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
        _buckets = new int[newSize];
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);

        TComparer? comparer = _comparer;
        for (int i = 0; i < count; i++)
        {
            if (entries[i].next >= -1)
            {
                ref int bucket = ref GetBucket(GetHashCode(entries[i].key, comparer));
                entries[i].next = bucket - 1; // Value in _buckets is 1-based
                bucket = i + 1;
            }
        }

        _entries = entries;
    }

    public bool Remove(TKey key)
    {
        // The overload Remove(TKey key, out TValue value) is a copy of this method with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "entries should be non-null");
            uint collisionCount = 0;

            TComparer? comparer = _comparer;
            uint hashCode = GetHashCode(key, comparer);

            ref int bucket = ref GetBucket(hashCode);
            Entry[]? entries = _entries;
            int last = -1;
            int i = bucket - 1; // Value in buckets is 1-based
            while (i >= 0)
            {
                ref Entry entry = ref entries[i];

                if (Equals(entry.key, key, comparer))
                {
                    entry.AssertHash(hashCode);

                    if (last < 0)
                    {
                        bucket = entry.next + 1; // Value in buckets is 1-based
                    }
                    else
                    {
                        entries[last].next = entry.next;
                    }

                    Debug.Assert((StartOfFreeList - _freeList) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
                    entry.next = StartOfFreeList - _freeList;

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                    {
                        entry.key = default!;
                    }

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                    {
                        entry.value = default!;
                    }

                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }
        return false;
    }

    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        // This overload is a copy of the overload Remove(TKey key) with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "entries should be non-null");
            uint collisionCount = 0;

            TComparer? comparer = _comparer;
            uint hashCode = GetHashCode(key, comparer);

            ref int bucket = ref GetBucket(hashCode);
            Entry[]? entries = _entries;
            int last = -1;
            int i = bucket - 1; // Value in buckets is 1-based
            while (i >= 0)
            {
                ref Entry entry = ref entries[i];

                if (Equals(entry.key, key, comparer))
                {
                    entry.AssertHash(hashCode);

                    if (last < 0)
                    {
                        bucket = entry.next + 1; // Value in buckets is 1-based
                    }
                    else
                    {
                        entries[last].next = entry.next;
                    }

                    value = entry.value;

                    Debug.Assert((StartOfFreeList - _freeList) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
                    entry.next = StartOfFreeList - _freeList;

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                    {
                        entry.key = default!;
                    }

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                    {
                        entry.value = default!;
                    }

                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.next;

                collisionCount++;
                if (collisionCount > (uint)entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }
            }
        }

        value = default;
        return false;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ref TValue valRef = ref FindValue(key);
        if (!Unsafe.IsNullRef(ref valRef))
        {
            value = valRef;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryAdd(TKey key, TValue value) => TryInsert(key, value, InsertionBehavior.None);

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();

    /// <summary>
    /// Ensures that the dictionary can hold up to 'capacity' entries without any further expansion of its backing storage
    /// </summary>
    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        int currentCapacity = _entries == null ? 0 : _entries.Length;
        if (currentCapacity >= capacity)
        {
            return currentCapacity;
        }

        _version++;

        if (_buckets == null)
        {
            return Initialize(capacity);
        }

        int newSize = HashHelpers.GetPrime(capacity);
        Resize(newSize, forceNewHashCodes: false);
        return newSize;
    }

    /// <summary>
    /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries
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
    public void TrimExcess() => TrimExcess(Count);

    /// <summary>
    /// Sets the capacity of this dictionary to hold up 'capacity' entries without any further expansion of its backing storage
    /// </summary>
    /// <remarks>
    /// This method can be used to minimize the memory overhead
    /// once it is known that no new elements will be added.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Passed capacity is lower than entries count.</exception>
    public void TrimExcess(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, Count);

        int newSize = HashHelpers.GetPrime(capacity);
        Entry[]? oldEntries = _entries;
        int currentCapacity = oldEntries == null ? 0 : oldEntries.Length;
        if (newSize >= currentCapacity)
        {
            return;
        }

        int oldCount = _count;
        _version++;
        Initialize(newSize);

        Debug.Assert(oldEntries is not null);

        CopyEntries(oldEntries, oldCount);
    }

    private void CopyEntries(Entry[] entries, int count)
    {
        Debug.Assert(_entries is not null);

        Entry[] newEntries = _entries;
        TComparer? comparer = _comparer;

        int newCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (entries[i].next >= -1)
            {
                uint hashCode = GetHashCode(entries[i].key, comparer);
                ref Entry entry = ref newEntries[newCount];
                entry = entries[i];
                ref int bucket = ref GetBucket(hashCode);
                entry.next = bucket - 1; // Value in _buckets is 1-based
                bucket = newCount + 1;
                newCount++;
            }
        }

        _count = newCount;
        _freeCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int GetBucket(uint hashCode)
    {
        int[] buckets = _buckets!;
        if (Unsafe.SizeOf<IntPtr>() == 8)
        {
            return ref buckets[HashHelpers.FastMod(hashCode, (uint)buckets.Length, _fastModMultiplier)];
        }
        else
        {
            return ref buckets[hashCode % (uint)buckets.Length];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetHashCode(TKey key, TComparer? comparer)
    {
        return (uint)((comparer == null) ? key.GetHashCode() : comparer.GetHashCode(key));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equals(TKey x, TKey y, TComparer? comparer)
    {
        return comparer == null ? EqualityComparer<TKey>.Default.Equals(x, y) : comparer.Equals(x, y);
    }

    private struct Entry
    {
#if DEBUG
        public uint hashCode;
#endif

        /// <summary>
        /// 0-based index of next entry in chain: -1 means end of chain
        /// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
        /// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
        /// </summary>
        public int next;
        public TKey key;     // Key of entry
        public TValue value; // Value of entry

        [Conditional("DEBUG")]
        public readonly void AssertHash(uint hashCode)
        {
#if DEBUG
            Debug.Assert(this.hashCode == hashCode);
#endif
        }
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly BucketDict<TKey, TValue, TComparer> _dictionary;
        private readonly int _version;
        private int _index;
        private KeyValuePair<TKey, TValue> _current;

        internal Enumerator(BucketDict<TKey, TValue, TComparer> dictionary)
        {
            _dictionary = dictionary;
            _version = dictionary._version;
            _index = 0;
            _current = default;
        }

        public bool MoveNext()
        {
            if (_version != _dictionary._version)
            {
                ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
            }

            // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
            // dictionary.count+1 could be negative if dictionary.count is int.MaxValue
            while ((uint)_index < (uint)_dictionary._count)
            {
                ref Entry entry = ref _dictionary._entries![_index++];

                if (entry.next >= -1)
                {
                    _current = new KeyValuePair<TKey, TValue>(entry.key, entry.value);
                    return true;
                }
            }

            _index = _dictionary._count + 1;
            _current = default;
            return false;
        }

        public readonly KeyValuePair<TKey, TValue> Current => _current;

        public void Dispose() { }

        readonly object? IEnumerator.Current => _current;

        public void Reset()
        {
            if (_version != _dictionary._version)
            {
                ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
            }

            _index = 0;
            _current = default;
        }
    }

    [DebuggerDisplay("Count = {Count}")]
    public readonly struct KeyCollection : ICollection<TKey>, IReadOnlyCollection<TKey>
    {
        private readonly BucketDict<TKey, TValue, TComparer> _dictionary;

        public KeyCollection(BucketDict<TKey, TValue, TComparer> dictionary)
        {
            ArgumentNullException.ThrowIfNull(dictionary);

            _dictionary = dictionary;
        }

        public Enumerator GetEnumerator() => new(_dictionary);

        public void CopyTo(TKey[] array, int index) => CopyTo(array.AsSpan(index));

        public void CopyTo(Span<TKey> destination)
        {
            if (destination.Length < _dictionary.Count)
            {
                ThrowHelper.ThrowArgumentException_DstTooSmall();
            }

            int index = 0;
            int count = _dictionary._count;
            Entry[]? entries = _dictionary._entries;
            for (int i = 0; i < count; i++)
            {
                if (entries![i].next >= -1)
                {
                    destination[index++] = entries[i].key;
                }
            }
        }

        public int Count => _dictionary.Count;

        bool ICollection<TKey>.IsReadOnly => true;

        void ICollection<TKey>.Add(TKey item) => ThrowHelper.ThrowNotSupportedException_KeyCollectionSet();

        void ICollection<TKey>.Clear() => ThrowHelper.ThrowNotSupportedException_KeyCollectionSet();

        public bool Contains(TKey item) => _dictionary.ContainsKey(item);

        bool ICollection<TKey>.Remove(TKey item)
        {
            ThrowHelper.ThrowNotSupportedException_KeyCollectionSet();
            return false;
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
        {
            return Count == 0 ? Enumerable.Empty<TKey>().GetEnumerator() : GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<TKey>)this).GetEnumerator();

        public struct Enumerator : IEnumerator<TKey>, IEnumerator
        {
            private readonly BucketDict<TKey, TValue, TComparer> _dictionary;
            private int _index;
            private readonly int _version;
            private TKey? _currentKey;

            internal Enumerator(BucketDict<TKey, TValue, TComparer> dictionary)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = 0;
                _currentKey = default;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
                }

                while ((uint)_index < (uint)_dictionary._count)
                {
                    ref Entry entry = ref _dictionary._entries![_index++];

                    if (entry.next >= -1)
                    {
                        _currentKey = entry.key;
                        return true;
                    }
                }

                _index = _dictionary._count + 1;
                _currentKey = default;
                return false;
            }

            public readonly TKey Current => _currentKey!;

            readonly object? IEnumerator.Current => _currentKey!;

            public void Reset()
            {
                if (_version != _dictionary._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
                }

                _index = 0;
                _currentKey = default;
            }
        }
    }

    [DebuggerDisplay("Count = {Count}")]
    public readonly struct ValueCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
    {
        private readonly BucketDict<TKey, TValue, TComparer> _dictionary;

        public ValueCollection(BucketDict<TKey, TValue, TComparer> dictionary)
        {
            ArgumentNullException.ThrowIfNull(dictionary);

            _dictionary = dictionary;
        }

        public Enumerator GetEnumerator() => new(_dictionary);

        public void CopyTo(TValue[] array, int index) => CopyTo(array.AsSpan(index));

        public void CopyTo(Span<TValue> destination)
        {
            if (destination.Length < _dictionary.Count)
            {
                ThrowHelper.ThrowArgumentException_DstTooSmall();
            }

            int index = 0;
            int count = _dictionary._count;
            Entry[]? entries = _dictionary._entries;
            for (int i = 0; i < count; i++)
            {
                if (entries![i].next >= -1)
                {
                    destination[index++] = entries[i].value;
                }
            }
        }

        public int Count => _dictionary.Count;

        bool ICollection<TValue>.IsReadOnly => true;

        void ICollection<TValue>.Add(TValue item) => ThrowHelper.ThrowNotSupportedException_ValueCollectionSet();

        bool ICollection<TValue>.Remove(TValue item)
        {
            ThrowHelper.ThrowNotSupportedException_ValueCollectionSet();
            return false;
        }

        void ICollection<TValue>.Clear() => ThrowHelper.ThrowNotSupportedException_ValueCollectionSet();

        bool ICollection<TValue>.Contains(TValue item) => _dictionary.ContainsValue(item);

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        {
            return Count == 0 ? Enumerable.Empty<TValue>().GetEnumerator() : GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<TValue>)this).GetEnumerator();

        public struct Enumerator : IEnumerator<TValue>, IEnumerator
        {
            private readonly BucketDict<TKey, TValue, TComparer> _dictionary;
            private int _index;
            private readonly int _version;
            private TValue? _currentValue;

            internal Enumerator(BucketDict<TKey, TValue, TComparer> dictionary)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = 0;
                _currentValue = default;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
                }

                while ((uint)_index < (uint)_dictionary._count)
                {
                    ref Entry entry = ref _dictionary._entries![_index++];

                    if (entry.next >= -1)
                    {
                        _currentValue = entry.value;
                        return true;
                    }
                }
                _index = _dictionary._count + 1;
                _currentValue = default;
                return false;
            }

            public readonly TValue Current => _currentValue!;

            readonly object? IEnumerator.Current => _currentValue;

            public void Reset()
            {
                if (_version != _dictionary._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_EnumFailedVersion();
                }

                _index = 0;
                _currentValue = default;
            }
        }
    }
}