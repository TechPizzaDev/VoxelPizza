using VoxelPizza.Collections;

namespace VoxelPizza.World.Octree;

// TODO: instead of contiguous leaves, flatten branches into grids (high entropy => get flattened)

public sealed class ChunkOctree : Octree<ChunkOctree.BranchHeader, byte>
{
    public ChunkOctree(int depth) : base(depth)
    {
    }

    protected override NestBranch? AllocNestBranch(NestBranch? parent, int depthLevel)
    {
        if (parent != null)
        {
            // TODO: return null if NestBranch has flat flag
        }

        NestBranch? branch = base.AllocNestBranch(parent, depthLevel);

        return branch;
    }

    protected override LeafBranch? AllocLeafBranch(NestBranch? parent)
    {
        LeafBranch? branch = base.AllocLeafBranch(parent);

        return branch;
    }

    public struct BranchHeader
    {
        private IndexMap<uint>? _palette;
        private byte _value;

    }
}