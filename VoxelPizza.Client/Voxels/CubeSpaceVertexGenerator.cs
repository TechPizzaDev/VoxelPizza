using System.Numerics;

namespace VoxelPizza.Client
{
    public struct CubeSpaceVertexGenerator : ICubeVertexGenerator<ChunkSpaceVertex>
    {
        public static uint BackNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitZ);
        public static uint BottomNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitY);
        public static uint FrontNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitZ);
        public static uint LeftNormal { get; } = ChunkSpaceVertex.PackNormal(-Vector3.UnitX);
        public static uint RightNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitX);
        public static uint TopNormal { get; } = ChunkSpaceVertex.PackNormal(Vector3.UnitY);

        public int MaxVerticesPerBlock => 4 * 6;

        public Vector3 Position { get; }

        public CubeSpaceVertexGenerator(Vector3 position)
        {
            Position = position;
        }

        public void AppendBack(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(1, 1, 0) + Position, BackNormal),
                new ChunkSpaceVertex(new Vector3(0, 1, 0) + Position, BackNormal),
                new ChunkSpaceVertex(new Vector3(0, 0, 0) + Position, BackNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 0) + Position, BackNormal));
        }

        public void AppendBottom(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(0, 0, 1) + Position, BottomNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 1) + Position, BottomNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 0) + Position, BottomNormal),
                new ChunkSpaceVertex(new Vector3(0, 0, 0) + Position, BottomNormal));
        }

        public void AppendFront(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(0, 1, 1) + Position, FrontNormal),
                new ChunkSpaceVertex(new Vector3(1, 1, 1) + Position, FrontNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 1) + Position, FrontNormal),
                new ChunkSpaceVertex(new Vector3(0, 0, 1) + Position, FrontNormal));
        }

        public void AppendLeft(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(0, 1, 0) + Position, LeftNormal),
                new ChunkSpaceVertex(new Vector3(0, 1, 1) + Position, LeftNormal),
                new ChunkSpaceVertex(new Vector3(0, 0, 1) + Position, LeftNormal),
                new ChunkSpaceVertex(new Vector3(0, 0, 0) + Position, LeftNormal));
        }

        public void AppendRight(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(1, 1, 1) + Position, RightNormal),
                new ChunkSpaceVertex(new Vector3(1, 1, 0) + Position, RightNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 0) + Position, RightNormal),
                new ChunkSpaceVertex(new Vector3(1, 0, 1) + Position, RightNormal));
        }

        public void AppendTop(ref ByteStore<ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkSpaceVertex(new Vector3(0, 1, 0) + Position, TopNormal),
                new ChunkSpaceVertex(new Vector3(1, 1, 0) + Position, TopNormal),
                new ChunkSpaceVertex(new Vector3(1, 1, 1) + Position, TopNormal),
                new ChunkSpaceVertex(new Vector3(0, 1, 1) + Position, TopNormal));
        }
    }
}
