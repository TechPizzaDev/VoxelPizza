using Xunit;

namespace VoxelPizza.World.Octree.Test;

public class OctreeTests
{
    public static TheoryData<int> DepthData => new(
    [
        0, 1, 2, 3, 4,
    ]);

    [Theory, MemberData(nameof(DepthData))]
    public void Insert(int depth)
    {
        Octree<float, float> tree = new(depth);
        int size = tree.Width;

        for (int y = 0; y < size; y++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    var iLeaf = tree.GetOrAdd(x, y, z);
                    var gLeaf = tree.Get(x, y, z);

                    Assert.Equal(iLeaf, gLeaf);
                }
            }
        }
    }
}
