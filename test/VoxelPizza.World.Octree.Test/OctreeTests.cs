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
        Octree<float, float> tree = new(depth);
        int size = tree.GetWidth();

        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var iLeaf = tree.GetOrAddLeaf(x, y, z);
                    Assert.True(iLeaf.HasValue);

                    var gLeaf = tree.GetLeaf(x, y, z);
                    Assert.Equal(iLeaf, gLeaf);
                }
            }
        }
    }

    [Theory, MemberData(nameof(DepthData))]
    public void NullGet(int depth)
    {
        Octree<float, float> tree = new(depth);
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
}
