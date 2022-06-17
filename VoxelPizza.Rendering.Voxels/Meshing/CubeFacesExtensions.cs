
namespace VoxelPizza.Client
{
    public static class CubeFacesExtensions
    {
        public static CubeFaces Opposite(this CubeFaces cubeFaces)
        {
            CubeFaces flipped = default;

            if ((cubeFaces & CubeFaces.Right) != 0)
                flipped |= CubeFaces.Left;

            if ((cubeFaces & CubeFaces.Left) != 0)
                flipped |= CubeFaces.Right;

            if ((cubeFaces & CubeFaces.Top) != 0)
                flipped |= CubeFaces.Bottom;

            if ((cubeFaces & CubeFaces.Bottom) != 0)
                flipped |= CubeFaces.Top;

            if ((cubeFaces & CubeFaces.Back) != 0)
                flipped |= CubeFaces.Front;

            if ((cubeFaces & CubeFaces.Front) != 0)
                flipped |= CubeFaces.Back;

            return flipped;
        }
    }
}
