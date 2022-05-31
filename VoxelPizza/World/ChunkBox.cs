using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public readonly partial struct ChunkBox : IEnumerable<ChunkPosition>
    {
        public readonly ChunkPosition Origin;
        public readonly ChunkPosition Max;

        public Size3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
               (uint)(Max.X - Origin.X),
               (uint)(Max.Y - Origin.Y),
               (uint)(Max.Z - Origin.Z));
        }

        public ChunkBox(ChunkPosition origin, ChunkPosition max)
        {
            Origin = origin;
            Max = max;
        }

        public ChunkBox(ChunkPosition origin, Size3 size)
        {
            Origin = origin;
            Max = origin + new ChunkPosition((int)size.W, (int)size.H, (int)size.D);
        }

        private static bool Intersects(
            ChunkPosition min1, ChunkPosition max1, ChunkPosition min2, ChunkPosition max2)
        {
            return min2.X < max1.X
                && min1.X < max2.X
                && min1.Y < max2.Y
                && min2.Y < max1.Y
                && min1.Z < max2.Z
                && min2.Z < max1.Z;
        }

        public bool Intersects(ChunkBox other)
        {
            ChunkPosition min1 = Origin;
            ChunkPosition max1 = Max;

            ChunkPosition min2 = other.Origin;
            ChunkPosition max2 = other.Max;

            return Intersects(min1, max1, min2, max2);
        }

        public bool Contains(ChunkPosition position)
        {
            return
                position.X >= Origin.X &&
                position.Y >= Origin.Y &&
                position.Z >= Origin.Z &&
                position.X < Max.X &&
                position.Y < Max.Y &&
                position.Z < Max.Z;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(Origin, Max);
        }

        IEnumerator<ChunkPosition> IEnumerable<ChunkPosition>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
