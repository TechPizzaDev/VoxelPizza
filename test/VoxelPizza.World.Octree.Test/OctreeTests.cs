using Xunit;

namespace VoxelPizza.World.Octree.Test;

public class OctreeTests
{
    private static int[] _depthData = [1, 2, 3, 4];

    public static TheoryData<int> DepthDataWithoutZero => new(_depthData);

    public static TheoryData<int> DepthData => new([0, .. _depthData]);

    [Theory, MemberData(nameof(DepthData))]
    public void AddThenGet(int depth)
    {
        Octree<int, int> tree = new(depth);
        int size = tree.GetWidth();
        int count = 1;

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
    }

    [Theory, MemberData(nameof(DepthData))]
    public void NullGet(int depth)
    {
        Octree<int, int> tree = new(depth);
        int size = tree.GetWidth();

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
    }

    [Theory, MemberData(nameof(DepthData))]
    public void AddThenTraverse(int depth)
    {
        Octree<int, int> tree = new(depth);
        int size = tree.GetWidth();
        int count = 1;
        int expected = 0;

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
