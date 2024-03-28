namespace VoxelPizza.Memory
{
    public struct ArenaSegment
    {
        public ulong Offset;
        public ulong Length;

        public readonly ulong End => Offset + Length;

        public ArenaSegment(ulong offset, ulong length)
        {
            Offset = offset;
            Length = length;
        }

        public override string ToString()
        {
            return $"{Offset} + {Length} -> {End}";
        }
    }
}
