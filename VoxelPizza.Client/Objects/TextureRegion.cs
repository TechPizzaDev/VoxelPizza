using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TextureRegion
    {
        public readonly uint TextureRgb;
        public readonly uint XY;
        public readonly uint EmissionRgb;
        public readonly uint Reserved;

        public TextureRegion(uint textureRgb, uint xy, uint emissionRgb)
        {
            TextureRgb = textureRgb;
            XY = xy;
            EmissionRgb = emissionRgb;

            Reserved = default;
        }

        public TextureRegion(byte texture, byte r, byte g, byte b, ushort x, ushort y, byte emR, byte emG, byte emB)
        {
            TextureRgb = texture | (uint)r << 8 | (uint)g << 16 | (uint)b << 24;
            XY = x | (uint)y << 16;
            EmissionRgb = emR | (uint)emG << 8 | (uint)emB << 16;

            Reserved = default;
        }

        public TextureRegion(byte texture, byte r, byte g, byte b, ushort x, ushort y) :
            this(texture, r, g, b, x, y, 0, 0, 0)
        {
        }
    }
}
