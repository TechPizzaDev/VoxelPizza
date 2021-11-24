using System.Runtime.CompilerServices;

namespace VoxelPizza.Numerics
{
    public static class IntMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideRoundDown(int a, int b)
        {
            return (a / b) + ((a % b) >> 31);
        }
    }
}
