using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Blocks;

public abstract partial class BlockStorage<T> : BlockStorage
    where T : IBlockStorageDescriptor
{
    public override sealed int Width => T.Width;
    public override sealed int Height => T.Height;
    public override sealed int Depth => T.Depth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint GetIndex(nuint x, nuint y, nuint z)
    {
        return GetIndexBase((uint)T.Depth, (uint)T.Width, y, z) + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(int x, int y, int z)
    {
        return GetIndexBase(T.Depth, T.Width, y, z) + x;
    }
}
