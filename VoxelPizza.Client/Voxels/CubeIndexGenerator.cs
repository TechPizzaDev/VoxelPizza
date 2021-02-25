namespace VoxelPizza.Client
{
    public struct CubeIndexGenerator : ICubeIndexGenerator<uint>
    {
        public int MaxIndicesPerBlock => 6 * 6;

        public static void AppendQuad(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            store.AppendRange(
                vertexOffset,
                vertexOffset + 1,
                vertexOffset + 2,
                vertexOffset,
                vertexOffset + 2,
                vertexOffset + 3);

            vertexOffset += 4;
        }

        public void AppendBack(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendBottom(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendFront(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendLeft(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendRight(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendTop(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }
    }
}
