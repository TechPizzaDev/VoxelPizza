namespace VoxelPizza.Collections
{
    public enum BlockStorageType : ushort
    {
        /// <summary>
        /// The backing storage is in an undefined state.
        /// </summary>
        Undefined,

        /// <summary>
        /// The backing storage is null.
        /// </summary>
        Null,

        /// <summary>
        /// The data is encoded and cannot be used directly.
        /// </summary>
        Specialized,

        /// <summary>
        /// A single value represents the entire data.
        /// </summary>
        Unsigned0,

        /// <summary>
        /// The data is stored in 8-bit unsigned integers.
        /// </summary>
        Unsigned8,

        /// <summary>
        /// The data is stored in 16-bit unsigned integers.
        /// </summary>
        Unsigned16,
        
        /// <summary>
        /// The data is stored in 24-bit unsigned integers.
        /// </summary>
        Unsigned24,
        
        /// <summary>
        /// The data is stored in 32-bit unsigned integers.
        /// </summary>
        Unsigned32
    }
}
