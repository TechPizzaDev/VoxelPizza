using System.Runtime.Intrinsics;

namespace VoxelPizza.Numerics;

public static class IntExtensions
{
    public static Vector128<int> AsVector128(this Int3 vector)
    {
        return Vector128.Create(vector.X, vector.Y, vector.Z, 0);
    }
    
    internal static Int3 AsInt3(this Vector128<int> vector)
    {
        return new Int3(vector.GetElement(0), vector.GetElement(1), vector.GetElement(2));
    }
}
