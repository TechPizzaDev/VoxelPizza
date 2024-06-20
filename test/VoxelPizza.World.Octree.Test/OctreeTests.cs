using System;
using Xunit;
using Xunit.Abstractions;

namespace VoxelPizza.World.Octree.Test;

public class OctreeTests(ITestOutputHelper output)
{
    private static int[] _depthData = [1, 2, 3, 4];

    public static TheoryData<int> DepthDataWithoutZero => new(_depthData);

    public static TheoryData<int> DepthData => new([0, .. _depthData]);

    private void Print<B, L>(Octree<B, L> tree, long startBytes1, long startBytes2)
    {
        long endBytes = GC.GetAllocatedBytesForCurrentThread();
        long total1 = endBytes - startBytes1;
        long total2 = endBytes - startBytes2;

        output.WriteLine($"Max capacity: {tree.GetMaxCapacity()}");
        output.WriteLine($"Bytes allocated (root): {total1}");
        output.WriteLine($"Bytes allocated: {total2}");

        if (tree is TrackingOctree<B, L> trackTree)
        {
            output.WriteLine($"Nest branches: {trackTree.NestBranchCount}");
            output.WriteLine($"Leaf branches: {trackTree.LeafBranchCount}");
        }
    }

    [Theory, MemberData(nameof(DepthData))]
    public void AddThenGet(int depth)
    {
        long startBytes1 = GC.GetAllocatedBytesForCurrentThread();
        TrackingOctree<int, int> tree = new(depth);
        int size = tree.GetWidth();
        int count = 1;

        long startBytes2 = GC.GetAllocatedBytesForCurrentThread();

        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var iLeaf = tree.GetOrAddLeaf(x, y, z);
                    Assert.True(iLeaf.HasValue);
                    Assert.Equal(0, iLeaf.Value);

                    iLeaf.Value = count;
                    count++;

                    var gLeaf = tree.GetLeaf(x, y, z);
                    Assert.Equal(iLeaf, gLeaf);
                    Assert.Equal(iLeaf.Value, gLeaf.Value);
                }
            }
        }

        Print(tree, startBytes1, startBytes2);
    }

    [Theory, MemberData(nameof(DepthData))]
    public void AddThenGetOne(int depth)
    {
        long startBytes1 = GC.GetAllocatedBytesForCurrentThread();
        TrackingOctree<int, int> tree = new(depth);
        int size = tree.GetWidth();
        int count = 1;

        long startBytes2 = GC.GetAllocatedBytesForCurrentThread();

        for (int x = 0; x < 1; x++)
        {
            var iLeaf = tree.GetOrAddLeaf(x, 0, 0);

            Assert.True(iLeaf.HasValue);
            Assert.Equal(0, iLeaf.Value);

            iLeaf.Value = count;
            count++;

            var gLeaf = tree.GetLeaf(x, 0, 0);
            Assert.Equal(iLeaf, gLeaf);
            Assert.Equal(iLeaf.Value, gLeaf.Value);
        }

        Print(tree, startBytes1, startBytes2);
    }

    [Theory, MemberData(nameof(DepthData))]
    public void NullGet(int depth)
    {
        long startBytes1 = GC.GetAllocatedBytesForCurrentThread();
        TrackingOctree<int, int> tree = new(depth);
        int size = tree.GetWidth();

        long startBytes2 = GC.GetAllocatedBytesForCurrentThread();

        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var gLeaf = tree.GetLeaf(x, y, z);
                    Assert.False(gLeaf.HasValue);
                }
            }
        }

        Print(tree, startBytes1, startBytes2);
    }

    [Theory, MemberData(nameof(DepthData))]
    public void AddThenTraverse(int depth)
    {
        long startBytes1 = GC.GetAllocatedBytesForCurrentThread();
        TrackingOctree<int, int> tree = new(depth);
        int size = tree.GetWidth();
        int count = 1;
        int expected = 0;

        long startBytes2 = GC.GetAllocatedBytesForCurrentThread();

        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var iLeaf = tree.GetOrAddLeaf(x, y, z);
                    Assert.True(iLeaf.HasValue);
                    Assert.Equal(0, iLeaf.Value);

                    iLeaf.Value = count;
                    expected += count;
                    count++;
                }
            }

            CountVisitor visitor = new();
            tree.Traverse(ref visitor);
            Assert.Equal(visitor.Count, expected);
        }

        Print(tree, startBytes1, startBytes2);
    }

    private struct CountVisitor : Octree<int, int>.IBranchVisitor
    {
        public int Count;

        public void VisitLeaf(Octree<int, int>.LeafBranch branch, int x, int y, int z)
        {
            foreach (int value in branch.AsSpan())
            {
                Count += value;
            }
        }

        public bool VisitNest(Octree<int, int>.NestBranch branch, int x, int y, int z, int depth)
        {
            return true;
        }
    }
}

class TrackingOctree<B, L> : Octree<B, L>
{
    public int LeafBranchCount;
    public int NestBranchCount;

    public TrackingOctree(int depth) : base(depth)
    {
    }

    protected override LeafBranch? AllocLeafBranch(NestBranch? parentBranch)
    {
        var branch = base.AllocLeafBranch(parentBranch);
        if (branch != null)
            LeafBranchCount++;
        return branch;
    }

    protected override NestBranch? AllocNestBranch(NestBranch? parentBranch, int depthLevel)
    {
        var branch = base.AllocNestBranch(parentBranch, depthLevel);
        if (branch != null)
            NestBranchCount++;
        return branch;
    }
}
