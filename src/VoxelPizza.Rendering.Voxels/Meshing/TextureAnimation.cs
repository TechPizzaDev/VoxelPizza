
namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public readonly struct TextureAnimation
    {
        public readonly uint Packed;

        public TextureAnimationType Type => (TextureAnimationType)(Packed & 1);
        public int StepCount => (int)(Packed >> 1 & 16383);
        public int StepRateRaw => (int)(Packed >> 15);
        public float StepRate => StepRateRaw / 4096f;

        public TextureAnimation(uint packed)
        {
            Packed = packed;
        }

        public static TextureAnimation CreateRaw(TextureAnimationType animationType, int stepCount, int stepRate)
        {
            return new TextureAnimation((uint)(
                (int)animationType & 1 |
                (stepCount & 16383) << 1 |
                (stepRate & 131071) << 15));
        }

        public static TextureAnimation Create(TextureAnimationType animationType, int stepCount, float stepRate)
        {
            return CreateRaw(animationType, stepCount, (int)(stepRate * 4096));
        }
    }
}
