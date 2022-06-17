using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public readonly struct WorldBox
    {
        public BlockPosition Origin { get; }
        public BlockPosition Max { get; }

        public Size3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
               (uint)(Max.X - Origin.X),
               (uint)(Max.Y - Origin.Y),
               (uint)(Max.Z - Origin.Z));
        }

        public WorldBox(BlockPosition origin, BlockPosition max)
        {
            Origin = origin;
            Max = max;
        }

        public WorldBox(BlockPosition origin, Size3 size)
        {
            Origin = origin;
            Max = origin + new BlockPosition((int)size.W, (int)size.H, (int)size.D);
        }

        private static bool Intersects(
            BlockPosition min1, BlockPosition max1, BlockPosition min2, BlockPosition max2)
        {
            return min2.X < max1.X
                && min1.X < max2.X
                && min1.Y < max2.Y
                && min2.Y < max1.Y
                && min1.Z < max2.Z
                && min2.Z < max1.Z;
        }

        public bool Intersects(WorldBox other)
        {
            BlockPosition min1 = Origin;
            BlockPosition max1 = Max;

            BlockPosition min2 = other.Origin;
            BlockPosition max2 = other.Max;

            return Intersects(min1, max1, min2, max2);
        }

        private static WorldBox Intersect(
            BlockPosition min1, BlockPosition max1, BlockPosition min2, BlockPosition max2)
        {
            int left_side = Math.Max(min1.X, min2.X);
            int right_side = Math.Min(max1.X, max2.X);
            uint w = (uint)(right_side - left_side);

            int bottom_side = Math.Max(min1.Y, min2.Y);
            int top_side = Math.Min(max1.Y, max2.Y);
            uint h = (uint)(top_side - bottom_side);

            int back_side = Math.Max(min1.Z, min2.Z);
            int front_side = Math.Min(max1.Z, max2.Z);
            uint d = (uint)(front_side - back_side);

            return new WorldBox(
                new BlockPosition(left_side, bottom_side, back_side),
                new Size3(w, h, d));
        }

        public WorldBox Intersect(WorldBox other)
        {
            BlockPosition min1 = Origin;
            BlockPosition max1 = Max;

            BlockPosition min2 = other.Origin;
            BlockPosition max2 = other.Max;

            return Intersect(min1, max1, min2, max2);
        }

        public bool TryIntersect(WorldBox other, out WorldBox intersection)
        {
            BlockPosition min1 = Origin;
            BlockPosition max1 = Max;

            BlockPosition min2 = other.Origin;
            BlockPosition max2 = other.Max;

            if (!Intersects(min1, max1, min2, max2))
            {
                intersection = default;
                return false;
            }

            intersection = Intersect(min1, max1, min2, max2);
            return true;
        }

        public ChunkBoxSliceEnumerator EnumerateChunkBoxSlices()
        {
            return new ChunkBoxSliceEnumerator(Origin, Max);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Origin, Max);
        }

        public override string ToString()
        {
            return $"{Origin} {Size}";
        }

        private string GetDebuggerDisplay()
        {
            return $"{Origin}  {Size}";
        }
    }
}
