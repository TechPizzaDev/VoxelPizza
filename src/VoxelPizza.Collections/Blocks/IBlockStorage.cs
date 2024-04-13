using System;

namespace VoxelPizza.Collections.Blocks
{
    public interface IBlockStorage
    {
        BlockStorageType StorageType { get; }
        int Width { get; }
        int Height { get; }
        int Depth { get; }
        bool IsEmpty { get; }

        /// <summary>
        /// Attempt to return a span for direct data access.
        /// </summary>
        /// <param name="inlineSpan">The span with direct data access.</param>
        /// <param name="storageType">The actual type of data backing the span.</param>
        /// <returns>Whether the <paramref name="inlineSpan"/> and <paramref name="storageType"/> is valid.</returns>
        bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType);
    }
}
