using System.Diagnostics;

namespace VoxelPizza.Client
{
    public struct CubeVertexProvider<TGenerator, T> : IVertexGenerator<T>
        where TGenerator : ICubeVertexGenerator<T>
        where T : unmanaged
    {
        public TGenerator Generator { get; }
        public CubeFaces Faces { get; }

        public CubeVertexProvider(TGenerator generator, CubeFaces faces)
        {
            Debug.Assert(generator != null);

            Generator = generator;
            Faces = faces;
        }

        public void AppendVertices(ref ByteStore<T> store)
        {
            store.PrepareCapacity(Generator.MaxVerticesPerBlock);

            if ((Faces & CubeFaces.Top) != 0)
                Generator.AppendTop(ref store);

            if ((Faces & CubeFaces.Bottom) != 0)
                Generator.AppendBottom(ref store);

            if ((Faces & CubeFaces.Left) != 0)
                Generator.AppendLeft(ref store);

            if ((Faces & CubeFaces.Right) != 0)
                Generator.AppendRight(ref store);

            if ((Faces & CubeFaces.Front) != 0)
                Generator.AppendFront(ref store);

            if ((Faces & CubeFaces.Back) != 0)
                Generator.AppendBack(ref store);
        }
    }
}
