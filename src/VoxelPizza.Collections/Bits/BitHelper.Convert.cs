using System.Numerics;

namespace VoxelPizza.Collections.Bits;

public static partial class BitHelper
{
    public static void Convert<S, D>(BitSpan<S> source, BitSpan<D> destination)
        where S : unmanaged, IBinaryInteger<S>
        where D : unmanaged, IBinaryInteger<D>
    {
        // TODO: optimize
        
        int length = source.Length;
        S elementMask = GetElementMask<S>(source.BitsPerElement);

        for (nint i = 0; i < length; i++)
        {
            S element = source.Get(i, elementMask);
            destination.Set(i, element, elementMask);
        }
    }
}
