using VoxelPizza.Rendering.Voxels.Meshing;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public struct StoredChunkMesh
    {
        public ChunkPosition Position;
        public ChunkPosition LocalPosition;
        public bool HasValue;

        public ChunkMeshResult StoredMesh;
        public int IsBuildRequired;
        public int IsRemoveRequired;

        public StoredChunkMesh(ChunkPosition position, ChunkPosition localPosition)
        {
            Position = position;
            LocalPosition = localPosition;
            HasValue = false;

            StoredMesh = default;
            IsBuildRequired = 0;
            IsRemoveRequired = 0;
        }
    }
}
