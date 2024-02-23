#if DEBUG
#define ALLOC_TRACK
#endif

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Memory
{
    public class ArenaAllocator
    {
        private List<ArenaSegment> _freeSegments = new();
#if ALLOC_TRACK
        private SortedList<uint, uint> _allocatedSegments = new();
#endif

        private uint _segmentsUsed;
        private uint _elementsUsed;

        public uint ElementCapacity { get; }

        public uint SegmentsUsed => _segmentsUsed;
        public uint SegmentsFree => (uint)_freeSegments.Count;
        public uint ElementsUsed => _elementsUsed;
        public uint ElementsFree => ElementCapacity - _elementsUsed;

        private ArenaAllocator()
        {
        }

        public ArenaAllocator(uint elementCapacity)
        {
            ElementCapacity = elementCapacity;

            ArenaSegment initialSegment = new(0, elementCapacity);
            _freeSegments.Add(initialSegment);
        }

        public ArenaAllocator Clone()
        {
            return new ArenaAllocator()
            {
                _freeSegments = new List<ArenaSegment>(_freeSegments),
#if ALLOC_TRACK
                _allocatedSegments = new SortedList<uint, uint>(_allocatedSegments),
#endif
                _segmentsUsed = _segmentsUsed,
                _elementsUsed = _elementsUsed,
            };
        }

        public bool TryAlloc(uint size, uint alignment, out ArenaSegment allocatedSegment)
        {
            if (_freeSegments.Count == 0)
            {
                allocatedSegment = default;
                return false;
            }

            Span<ArenaSegment> freeSegments = CollectionsMarshal.AsSpan(_freeSegments);

            uint alignedSegmentSize = 0;
            uint alignedOffsetRemainder = 0;

            int i = 0;
            int selectedIndex = -1;
            for (; i < freeSegments.Length; i++)
            {
                ref ArenaSegment segment = ref freeSegments[i];
                alignedSegmentSize = segment.Length;
                alignedOffsetRemainder = segment.Offset % alignment;
                if (alignedOffsetRemainder != 0)
                {
                    uint alignmentCorrection = alignment - alignedOffsetRemainder;
                    if (alignedSegmentSize <= alignmentCorrection)
                    {
                        continue;
                    }
                    alignedSegmentSize -= alignmentCorrection;
                }

                if (alignedSegmentSize >= size) // Valid match -- split it and return.
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex != -1)
            {
                ArenaSegment segment = freeSegments[selectedIndex];
                segment.Length = alignedSegmentSize;
                if (alignedOffsetRemainder != 0)
                {
                    segment.Offset += alignment - alignedOffsetRemainder;
                }

                if (alignedSegmentSize != size)
                {
                    ArenaSegment splitSegment = new(
                        segment.Offset + size,
                        segment.Length - size);

                    freeSegments[selectedIndex] = splitSegment;
                    segment.Length = size;
                }
                else
                {
                    _freeSegments.RemoveAt(i);
                }

#if ALLOC_TRACK
                CheckAllocatedSegment(segment);
#endif
                _segmentsUsed++;
                _elementsUsed += segment.Length;
                allocatedSegment = segment;
                return true;
            }

#if ALLOC_TRACK
            bool hasMerged = MergeContiguousSegments();
            TrackAssert(!hasMerged, "Free method was not effective at merging segments.");
#endif

            allocatedSegment = default;
            return false;
        }

        private static int FindPrecedingSegmentIndex(ReadOnlySpan<ArenaSegment> list, uint targetOffset)
        {
            int low = 0;
            int high = list.Length - 1;
            ref ArenaSegment s = ref MemoryMarshal.GetReference(list);

            if (list.Length == 0 || Unsafe.Add(ref s, high).Offset < targetOffset)
                return -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);

                if (Unsafe.Add(ref s, mid).Offset >= targetOffset)
                    high = mid - 1;
                else
                    low = mid + 1;
            }

            return high + 1;
        }

        public void Free(ArenaSegment segment)
        {
            Span<ArenaSegment> freeSegment = CollectionsMarshal.AsSpan(_freeSegments);

            // The free segment list should always be sorted by offset.
            // List mutations done by this algorithm must preserve order. 

            int precedingSegment = FindPrecedingSegmentIndex(freeSegment, segment.Offset);
            if (precedingSegment != -1)
            {
                if ((uint)precedingSegment < (uint)freeSegment.Length &&
                    segment.End == freeSegment[precedingSegment].Offset)
                {
                    // Free segment ends at the beginning of found segment; merge with found segment.
                    freeSegment[precedingSegment].Length += segment.Length;
                    freeSegment[precedingSegment].Offset = segment.Offset;

                    int prevSegment = precedingSegment - 1;
                    if ((uint)prevSegment < (uint)freeSegment.Length &&
                        freeSegment[prevSegment].End == freeSegment[precedingSegment].Offset)
                    {
                        // Merged segment begins at the end of the previous segment; extend the previous segment.
                        freeSegment[prevSegment].Length += freeSegment[precedingSegment].Length;
                        _freeSegments.RemoveAt(precedingSegment);
                    }
                }
                else
                {
                    int prevSegment = precedingSegment - 1;
                    if ((uint)prevSegment < (uint)freeSegment.Length &&
                        freeSegment[prevSegment].End == segment.Offset)
                    {
                        // Free segment begins at the end of found segment; extend the previous segment.
                        freeSegment[prevSegment].Length += segment.Length;
                    }
                    else
                    {
                        // Free segment could not be merged with previous or found segment.
                        _freeSegments.Insert(precedingSegment, segment);
                    }
                }
            }
            else
            {
                int lastIndex = freeSegment.Length - 1;
                if ((uint)lastIndex < (uint)freeSegment.Length &&
                    freeSegment[lastIndex].End == segment.Offset)
                {
                    // Free segment begins at the end of last segment; extend the last segment.
                    freeSegment[lastIndex].Length += segment.Length;
                }
                else
                {
                    // Free segment is at the end of the list.
                    _freeSegments.Add(segment);
                }
            }

            _segmentsUsed--;
            _elementsUsed -= segment.Length;
#if ALLOC_TRACK
            RemoveAllocatedSegment(segment);
#endif
        }

#if ALLOC_TRACK
        private bool MergeContiguousSegments()
        {
            List<ArenaSegment> freeSegments = _freeSegments;
            bool hasMerged = false;
            int contiguousLength = 1;

            for (int i = 0; i < freeSegments.Count - 1; i++)
            {
                uint segmentStart = freeSegments[i].Offset;
                while (i + contiguousLength < freeSegments.Count
                    && freeSegments[i + contiguousLength - 1].End == freeSegments[i + contiguousLength].Offset)
                {
                    contiguousLength += 1;
                }

                if (contiguousLength > 1)
                {
                    ulong segmentEnd = freeSegments[i + contiguousLength - 1].End;
                    freeSegments.RemoveRange(i, contiguousLength);

                    ArenaSegment mergedSegment = new(
                        segmentStart,
                        (uint)(segmentEnd - segmentStart));

                    freeSegments.Insert(i, mergedSegment);
                    hasMerged = true;
                    contiguousLength = 0;
                }
            }

            return hasMerged;
        }

        private void CheckAllocatedSegment(ArenaSegment segment)
        {
            _allocatedSegments.Add(segment.Offset, segment.Length); // Throws on same key added twice.

            int index = _allocatedSegments.IndexOfKey(segment.Offset);

            if (index > 0)
            {
                ArenaSegment leftSegment = new(_allocatedSegments.Keys[index - 1], _allocatedSegments.Values[index - 1]);
                TrackAssert(!IsSegmentOverlap(segment, leftSegment), "Allocated segments have overlapped.");
            }

            if (index < _allocatedSegments.Count - 1)
            {
                ArenaSegment rightSegment = new(_allocatedSegments.Keys[index + 1], _allocatedSegments.Values[index + 1]);
                TrackAssert(!IsSegmentOverlap(segment, rightSegment), "Allocated segments have overlapped.");
            }
        }

        private static bool IsSegmentOverlap(ArenaSegment first, ArenaSegment second)
        {
            ulong firstStart = first.Offset;
            ulong firstEnd = first.Offset + first.Length;
            ulong secondStart = second.Offset;
            ulong secondEnd = second.Offset + second.Length;

            return (firstStart <= secondStart && firstEnd > secondStart
                || firstStart >= secondStart && firstEnd <= secondEnd
                || firstStart < secondEnd && firstEnd >= secondEnd
                || firstStart <= secondStart && firstEnd >= secondEnd);
        }

        private void RemoveAllocatedSegment(ArenaSegment segment)
        {
            TrackAssert(_allocatedSegments.Remove(segment.Offset), "Unable to remove a supposedly allocated segment.");
        }

        private static void TrackAssert(bool condition, string message)
        {
            if (!condition)
            {
                ThrowTrackException(message);
            }
        }

        private static void ThrowTrackException(string message)
        {
            throw new InvalidOperationException(message);
        }
#endif

        public bool IsFullFreeSegment()
        {
            bool IsFirstFullFree()
            {
                ArenaSegment freeSegment = _freeSegments[0];
                return freeSegment.Offset == 0
                    && freeSegment.Length == ElementCapacity;
            }

            if (_freeSegments.Count == 1)
            {
                return IsFirstFullFree();
            }
            return false;
        }
    }
}
