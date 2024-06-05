using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.World.Octree;

/// <summary>
/// </summary>
/// <remarks>
/// Heavily based on <see href="https://github.com/mwarning/SimpleOctree" />, 
/// modified to not store leaves as individual nodes.
/// </remarks>
/// <typeparam name="B">Value stored in each branch.</typeparam>
/// <typeparam name="L">Value stored in each leaf.</typeparam>
public class Octree<B, L>
{
    private readonly int _depth;

    private Branch? _root;
    private int _branchCount;
    private int _leafCount;

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
        return 1 << (_depth + 1);
    }

    /// <summary>
    /// Maximum number of leaves.
    /// </summary>
    public int GetMaxCapacity()
    {
        int w = GetWidth();
        return w * w * w;
    }

    /// <summary>
    /// Get a <see cref="Leaf"/> at the given position if the branch exists.
    /// </summary>
    /// <returns>The <see cref="Leaf"/> if the branch exists, otherwise an empty <see cref="Leaf"/>.</returns>
    public Leaf GetLeaf(int x, int y, int z)
    {
        uint width = (uint)GetWidth();
        Debug.Assert((uint)x < width);
        Debug.Assert((uint)y < width);
        Debug.Assert((uint)z < width);

        Branch? branch = _root;
        int mask = 1 << _depth;

        while (mask != 1 && branch != null)
        {
            Debug.Assert(branch is NestBranch);

            mask >>>= 1;
            int index = GetIndex(x, y, z, mask);
            branch = Unsafe.As<NestBranch>(branch).GetBranch(index);
        }

        if (branch == null)
        {
            return new Leaf();
        }
        Debug.Assert(branch is LeafBranch);

        int leafIndex = GetIndex(x, y, z, 1);
        return new Leaf(Unsafe.As<LeafBranch>(branch), leafIndex);
    }

    /// <summary>
    /// Get or add a <see cref="Leaf"/> at the given position.
    /// </summary>
    /// <returns>The <see cref="Leaf"/>.</returns>
    public Leaf GetOrAddLeaf(int x, int y, int z)
    {
        uint width = (uint)GetWidth();
        Debug.Assert((uint)x < width);
        Debug.Assert((uint)y < width);
        Debug.Assert((uint)z < width);

        ref Branch? branch = ref _root;
        int depth = _depth;

        while (depth > 0)
        {
            if (branch == null)
            {
                branch = AllocNestBranch();
            }
            Debug.Assert(branch is NestBranch);

            depth--;

            int mask = 1 << depth;
            int index = GetIndex(x, y, z, mask);
            branch = ref Unsafe.As<NestBranch>(branch).GetBranch(index);
        }

        if (branch == null)
        {
            Debug.Assert(depth == 0);
            branch = new LeafBranch();
            _leafCount++;
        }
        Debug.Assert(branch is LeafBranch);

        int leafIndex = GetIndex(x, y, z, 1);
        return new Leaf(Unsafe.As<LeafBranch>(branch), leafIndex);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private NestBranch AllocNestBranch()
    {
        NestBranch branch = new();
        _branchCount++;
        return branch;
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

    public readonly struct Leaf(LeafBranch parent, int index) : IEquatable<Leaf>
    {
        public LeafBranch Parent { get; } = parent;
        public int Index { get; } = index;

        public bool IsEmpty => Parent == null;
        public ref L Value => ref Parent.GetLeaf(Index);

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

        [InlineArray(8)]
        private struct Array
        {
            private Branch? _e0;
        }
    }
}
