using System;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public struct RenderOrderKey : IComparable<RenderOrderKey>
    {
        public readonly ulong Value;

        public RenderOrderKey(ulong value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderOrderKey Create(int materialID, float cameraDistance)
            => Create((uint)materialID, cameraDistance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderOrderKey Create(uint materialID, float cameraDistance)
        {
            uint cameraDistanceInt = (uint)Math.Min(uint.MaxValue, (cameraDistance * 1000f));

            return new RenderOrderKey(
                ((ulong)materialID << 32) +
                cameraDistanceInt);
        }

        public int CompareTo(RenderOrderKey other)
        {
            return Value.CompareTo(other.Value);
        }
    }
}