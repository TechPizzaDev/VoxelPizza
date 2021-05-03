using System;

namespace VoxelPizza.Client
{
    [Flags]
    public enum BlockVisualFeatures : byte
    {
        None = 0,

        /// <summary>
        /// The block can cull individual sides.
        /// </summary>
        CullableSides = 1 << 0,

        /// <summary>
        /// The block can be either fully visible or fully culled.
        /// </summary>
        CullableVisibility = 1 << 1,

        /// <summary>
        /// The block can be culled.
        /// </summary>
        CullableAny = CullableSides | CullableVisibility,

        /// <summary>
        /// The block emits light.
        /// </summary>
        LightEmitter = 1 << 2,
    }
}