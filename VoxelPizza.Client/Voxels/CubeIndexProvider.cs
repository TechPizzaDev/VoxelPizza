using System.Diagnostics;

namespace VoxelPizza.Client
{
    public struct CubeIndexProvider<TGenerator, T> : IIndexGenerator<T>
        where TGenerator : ICubeIndexGenerator<T>
        where T : unmanaged
    {
        public TGenerator Generator { get; }
        public CubeFaces Faces { get; }

        public CubeIndexProvider(TGenerator generator, CubeFaces faces)
        {
            Debug.Assert(generator != null);

            Generator = generator;
            Faces = faces;
        }

        public void AppendIndices(ref ByteStore<T> store, ref uint vertexOffset)
        {
            store.PrepareCapacity(Generator.MaxIndicesPerBlock);

            if ((Faces & CubeFaces.Top) != 0)
                Generator.AppendTop(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Bottom) != 0)
                Generator.AppendBottom(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Left) != 0)
                Generator.AppendLeft(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Right) != 0)
                Generator.AppendRight(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Front) != 0)
                Generator.AppendFront(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Back) != 0)
                Generator.AppendBack(ref store, ref vertexOffset);
        }
    }
}
