using System.Numerics;

namespace VoxelPizza.Numerics
{
    public struct AlignedBoxCorners4
    {
        public const int CornerCount = 8;

        public Vector4 NearTopLeft;
        public Vector4 NearTopRight;
        public Vector4 NearBottomLeft;
        public Vector4 NearBottomRight;
        public Vector4 FarTopLeft;
        public Vector4 FarTopRight;
        public Vector4 FarBottomLeft;
        public Vector4 FarBottomRight;
    }
}