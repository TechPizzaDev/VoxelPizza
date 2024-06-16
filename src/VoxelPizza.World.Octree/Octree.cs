using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace VoxelPizza.World.Octree;

/// <summary>
/// </summary>
/// <remarks>
/// Heavily based on <see href="https://github.com/mwarning/SimpleOctree" />, 
/// modified to not store leaves as individual nodes.
/// </remarks>
/// <typeparam name="B">Value stored in each <see cref="Branch">.</typeparam>
/// <typeparam name="L">Value stored in each leaf.</typeparam>
public class Octree<B, L>
{
    private readonly int _depth;

    private Branch? _root;

    /// <summary>
    /// Maximum depth of the tree.
    /// </summary>
    public int Depth => _depth;

    public Octree(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        _depth = depth;
    }

    /// <summary>
    /// Size of the bounding box, serving as the exclusive bound for coordinates.
    /// </summary>
    /// <remarks>
    /// Always a power of two.
    /// </remarks>
    public int GetWidth()
    {
        // LeafBranch is a contiguous 2x2x2 array,
        // so that's the smallest we can go since (1 << (0 + 1)) equals 2.

        return 1 << (_depth + 1);
    }

    /// <summary>
    /// Maximum number of leaf values.
    /// </summary>
    public int GetMaxCapacity()
    {
        int w = GetWidth();
        return w * w * w;
    }

    /// <summary>
    /// Get a leaf at the given position.
    /// </summary>
    /// <returns>The leaf if the branch exists; otherwise a leaf with no value.</returns>
    public Leaf GetLeaf(int x, int y, int z)
    {
        uint width = (uint)GetWidth();
        Debug.Assert((uint)x < width);
        Debug.Assert((uint)y < width);
        Debug.Assert((uint)z < width);

        Branch? branch = _root;
        NestBranch? parent = null;
        int depth = _depth;

        while (depth > 0 && branch != null)
        {
            Debug.Assert(branch is NestBranch);

            parent = Unsafe.As<NestBranch>(branch);
            depth--;

            int mask = 2 << depth;
            int index = GetIndex(x, y, z, mask);
            branch = parent.GetBranch(index);
        }

        Debug.Assert(branch == null || branch is LeafBranch);

        int leafIndex = GetIndex(x, y, z, 1);
        return new Leaf(Unsafe.As<LeafBranch>(branch), leafIndex);
    }

    /// <summary>
    /// Get or add a leaf at the given position.
    /// </summary>
    /// <returns>The leaf if all branch allocations succeed; otherwise a leaf with no value.</returns>
    public Leaf GetOrAddLeaf(int x, int y, int z)
    {
        uint width = (uint)GetWidth();
        Debug.Assert((uint)x < width);
        Debug.Assert((uint)y < width);
        Debug.Assert((uint)z < width);

        ref Branch? branch = ref _root;
        NestBranch? parent = null;
        int depth = _depth;

        while (depth > 0)
        {
            int depthLevel = _depth - depth;
            if (branch == null)
            {
                branch = AllocNestBranch(parent, depthLevel);
                if (branch == null)
                {
                    goto Return;
                }
            }
            Debug.Assert(branch is NestBranch);

            parent = Unsafe.As<NestBranch>(branch);
            depth--;

            int mask = 2 << depth;
            int index = GetIndex(x, y, z, mask);
            branch = ref parent.GetBranch(index);
        }

        Debug.Assert(depth == 0);
        if (branch == null)
        {
            branch = AllocLeafBranch(parent);
        }

        Return:
        Debug.Assert(branch == null || branch is LeafBranch);

        int leafIndex = GetIndex(x, y, z, 1);
        return new Leaf(Unsafe.As<LeafBranch>(branch), leafIndex);
    }

    protected virtual NestBranch? AllocNestBranch(NestBranch? parent, int depthLevel)
    {
        return new NestBranch();
    }

    protected virtual LeafBranch? AllocLeafBranch(NestBranch? parent)
    {
        return new LeafBranch();
    }

    private static int GetIndex(int x, int y, int z, int mask)
    {
        // The Nth bit of x, y and z is encoded in the index.
        // Since size is always a power of two, size has always
        // only one bit set and it is used as bit mask to check the Nth bit.

        return
            ((x & mask) != 0 ? 1 : 0) * 1 +
            ((z & mask) != 0 ? 1 : 0) * 2 +
            ((y & mask) != 0 ? 1 : 0) * 4;
    }

    public readonly struct Leaf : IEquatable<Leaf>
    {
        public LeafBranch? Parent { get; }
        public int Index { get; }

        internal Leaf(LeafBranch? parent, int index)
        {
            Parent = parent;
            Index = index;
        }

        [MemberNotNullWhen(true, nameof(Parent))]
        public bool HasValue => Parent != null;

        public ref L Value
        {
            get
            {
                Debug.Assert(Parent != null);
                return ref Parent.GetLeaf(Index);
            }
        }

        public bool Equals(Leaf other)
        {
            return Parent == other.Parent && Index == other.Index;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Parent?.GetHashCode() ?? 0, Index);
        }
    }

    public abstract class Branch
    {
        public B? Value;
    }

    public sealed class LeafBranch : Branch
    {
        private Array _leaves;

        public ref L GetLeaf(int index) => ref _leaves[index];

        public Span<L> AsSpan() => _leaves;

        [InlineArray(8)]
        private struct Array
        {
            private L _e0;
        }
    }

    public sealed class NestBranch : Branch
    {
        private Array _branches;

        public ref Branch? GetBranch(int index) => ref _branches[index];

        public Span<Branch?> AsSpan() => _branches;

        [InlineArray(8)]
        private struct Array
        {
            private Branch? _e0;
        }
    }
}
