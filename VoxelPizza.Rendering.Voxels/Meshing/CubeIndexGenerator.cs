using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public readonly unsafe struct CubeIndexGenerator : ICubeIndexGenerator<uint>
    {
        public uint MaxIndices => 6 * 6;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendQuad(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            uint* ptr = store.GetAppendPtr(6);
            ptr[0] = vertexOffset;
            ptr[1] = vertexOffset + 1;
            ptr[2] = vertexOffset + 2;
            ptr[3] = vertexOffset;
            ptr[4] = vertexOffset + 2;
            ptr[5] = vertexOffset + 3;

            vertexOffset += 4;
        }

        public void AppendLast(ref ByteStore<uint> store, ref uint vertexOffset)
        {
        }

        public void AppendFirst(ref ByteStore<uint> store, ref uint vertexOffset)
        {
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
