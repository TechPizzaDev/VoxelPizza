using System;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    [Flags]
    public enum BlockVisualFeatures : byte
    {
        None = 0,

        /// <summary>
        /// The meshing requests nearest neighbors.
        /// </summary>
        NeighborNear = 1 << 0,

        NeighborFar = 1 << 1,

        /// <summary>
        /// The meshing requests 3x3 of neighbor blocks around the center.
        /// </summary>
        NeighborCorners = 1 << 2,

        NeighborAll = NeighborNear | NeighborFar | NeighborCorners,

        /// <summary>
        /// The mesher acts based on the nearest neighboring faces.
        /// </summary>
        FaceDependent = 1 << 3 | NeighborNear,

        /// <summary>
        /// Determines whether meshing should be skipped early when lacking any exposed faces.
        /// </summary>
        SkipIfObstructed = 1 << 4 | FaceDependent,

        /// <summary>
        /// The block emits light.
        /// </summary>
        LightEmitter = 1 << 5,
    }
}