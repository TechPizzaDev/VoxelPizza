namespace VoxelPizza.Memory
{
    public struct ArenaSegment
    {
        public uint Offset;
        public uint Length;

        public readonly ulong End => Offset + Length;

        public ArenaSegment(uint offset, uint length)
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
