using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.World.Octree;

public class Octree<B, L>
{
    private readonly int _depth;

    private Node? _root;
    private int _branchCount;
    private int _leafCount;

    /// <summary>
    /// Maximum depth of the tree.
    /// </summary>
    public int Depth => _depth;

    /// <summary>
    /// Size of the bounding box.
    /// Always a power of two.
    /// </summary>
    public int Width => 1 << _depth;

    public Octree(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        _depth = depth;
    }

    /// <summary>
    /// Maximum number of leaves.
    /// </summary>
    public int GetMaxCapacity()
    {
        int w = Width;
        return w * w * w;
    }

    /// <summary>
    /// Get a <see cref="Leaf"/> at the given position if it exists.
    /// </summary>
    /// <returns>A <see cref="Leaf"/> if it exists, otherwise <see langword="null"/>.</returns>
    public Leaf? Get(int x, int y, int z)
    {
        Node? node = _root;
        int size = Width;

        Debug.Assert(x < size);
        Debug.Assert(y < size);
        Debug.Assert(z < size);

        while (size != 1 && node != null)
        {
            size /= 2;
            int index = GetIndex(x, y, z, size);
            node = Unsafe.As<Branch>(node).GetNode(index);
        }

        return Unsafe.As<Leaf>(node);
    }

    /// <summary>
    /// Get or add a <see cref="Leaf "/> at the given position.
    /// </summary>
    /// <returns>The <see cref="Leaf"/>.</returns>
    public Leaf GetOrAdd(int x, int y, int z)
    {
        ref Node? node = ref _root;
        int depth = _depth;
        int width = Width;

        Debug.Assert(x < width);
        Debug.Assert(y < width);
        Debug.Assert(z < width);

        while (depth > 0)
        {
            if (node == null)
            {
                node = AllocBranch();
            }

            --depth;

            int size = 1 << depth;
            int index = GetIndex(x, y, z, size);
            node = ref Unsafe.As<Branch>(node).GetNode(index);
        }

        if (node == null)
        {
            Debug.Assert(depth == 0);
            node = new Leaf();
            _leafCount++;
        }

        return Unsafe.As<Leaf>(node);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Branch AllocBranch()
    {
        Branch branch = new();
        _branchCount++;
        return branch;
    }

    private static int GetIndex(int x, int y, int z, int size)
    {
        // The nth bit of x, y and z is encoded in the index.
        // Since size is always a power of two, size has always
        // only one bit set and it is used as bit mask to check the nth bit.

        return
            ((x & size) != 0 ? 1 : 0) * 1 +
            ((z & size) != 0 ? 1 : 0) * 2 +
            ((y & size) != 0 ? 1 : 0) * 4;
    }

    public abstract class Node
    {
    }

    public sealed class Leaf : Node
    {
        public L? Value;
    }

    public sealed class Branch : Node
    {
        private NodeArray _nodes;
        public B? Value;

        internal ref Node? GetNode(int index) => ref _nodes[index];
    }

    [InlineArray(8)]
    [StructLayout(LayoutKind.Sequential)]
    public struct NodeArray
    {
        private Node? _e0;
    }
}
