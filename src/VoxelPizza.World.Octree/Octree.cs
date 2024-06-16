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

    /// <summary>
    /// Walk through every non-null branch of the tree.
    /// </summary>
    /// <typeparam name="T">The type of the visitor implementation.</typeparam>
    /// <param name="visitor">The visitor implementation that will accept branches.</param>
    public void Traverse<T>(ref T visitor)
        where T : IBranchVisitor
    {
        if (_root != null)
        {
            Traverse(ref visitor, _root, 0, 0, 0, _depth);
        }
    }

    /// <summary>
    /// Allocate a new branch for nested branches.
    /// </summary>
    /// <param name="parentBranch">The parent branch where the new branch will be stored.</param>
    /// <param name="depthLevel">The depth of the new branch.</param>
    /// <returns>A new branch, or <see langword="null"/> which will stop traversal.</returns>
    protected virtual NestBranch? AllocNestBranch(NestBranch? parentBranch, int depthLevel)
    {
        return new NestBranch();
    }

    /// <summary>
    /// Allocate a new branch for leaf values.
    /// </summary>
    /// <param name="parentBranch">The parent branch where the new branch will be stored.</param>
    /// <returns>A new branch, or <see langword="null"/> which will stop traversal.</returns>
    protected virtual LeafBranch? AllocLeafBranch(NestBranch? parentBranch)
    {
        return new LeafBranch();
    }

    private static void Traverse<T>(ref T visitor, Branch branch, int x, int y, int z, int depth)
        where T : IBranchVisitor
    {
        Debug.Assert(branch != null);

        if (depth == 0)
        {
            Debug.Assert(branch is LeafBranch);

            visitor.VisitLeaf(Unsafe.As<LeafBranch>(branch), x, y, z);
            return;
        }
        Debug.Assert(branch is NestBranch);

        NestBranch parent = Unsafe.As<NestBranch>(branch);
        if (!visitor.VisitNest(parent, x, y, z, depth))
        {
            return;
        }

        depth--;
        int mask = 2 << depth;

        Span<Branch?> children = parent.AsSpan();
        for (int i = 0; i < children.Length; i++)
        {
            Branch? child = children[i];
            if (child == null)
            {
                continue;
            }

            (int tX, int tY, int tZ) = GetCoords(x, y, z, i, mask);
            Traverse(ref visitor, child, tX, tY, tZ, depth);
        }
    }

    private static (int x, int y, int z) GetCoords(int x, int y, int z, int i, int mask)
    {
        Debug.Assert(i < 8);

        //int cX = (i & 1) != 0 ? (x | mask) : x;
        //int cZ = (i & 2) != 0 ? (z | mask) : z;
        //int cY = (i & 4) != 0 ? (y | mask) : y;

        int cX = x | (((i << 31) >> 31) & mask);
        int cZ = z | (((i << 30) >> 31) & mask);
        int cY = y | (((i << 29) >> 31) & mask);
        return (cX, cY, cZ);
    }

    private static int GetIndex(int x, int y, int z, int mask)
    {
        // The Nth bit of x, y and z is encoded in the index.
        // Since size is always a power of two, size has always
        // only one bit set and it is used as bit mask to check the Nth bit.

        int iX = ((x & mask) != 0 ? 1 : 0) * 1;
        int iZ = ((z & mask) != 0 ? 1 : 0) * 2;
        int iY = ((y & mask) != 0 ? 1 : 0) * 4;
        return iX + iZ + iY;
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

    [DebuggerTypeProxy(typeof(LeafBranchDebugView<,>))]
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

    [DebuggerTypeProxy(typeof(NestBranchDebugView<,>))]
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

    /// <summary>
    /// Used for traversing branches with <see cref="Traverse{T}(ref T)"/>.
    /// </summary>
    public interface IBranchVisitor
    {
        /// <summary>
        /// Visits a branch that contains nested branches, optionally skipping it.
        /// </summary>
        /// <param name="branch">The branch with nested branches.</param>
        /// <param name="x">X coordinate of the branch.</param>
        /// <param name="x">Y coordinate of the branch.</param>
        /// <param name="x">Z coordinate of the branch.</param>
        /// <param name="depth">The depth of the branch.</param>
        /// <returns>
        /// <see langword="true"/> to visit the branch; 
        /// <see langword="false"/> to skip it and nested branches.
        /// </returns>
        bool VisitNest(NestBranch branch, int x, int y, int z, int depth);

        /// <summary>
        /// Visits a branch with leaf values.
        /// </summary>
        /// <param name="branch">The leaf branch.</param>
        /// <param name="x">X coordinate of the branch.</param>
        /// <param name="x">Y coordinate of the branch.</param>
        /// <param name="x">Z coordinate of the branch.</param>
        void VisitLeaf(LeafBranch branch, int x, int y, int z);
    }
}

internal sealed class NestBranchDebugView<B, L>(Octree<B, L>.NestBranch branch)
{
    public ref B? Value => ref branch.Value;
    public Span<Octree<B, L>.Branch?> Branches => branch.AsSpan();
}

internal sealed class LeafBranchDebugView<B, L>(Octree<B, L>.LeafBranch branch)
{
    public ref B? Value => ref branch.Value;
    public Span<L> Leaves => branch.AsSpan();
}
