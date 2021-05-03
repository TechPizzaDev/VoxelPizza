using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public class ChunkMesher
    {
        private BlockEliminaryDescription[] descs;
        private TextureAnimation[] anims;
        private MeshProvider?[] meshProviders;

        public HeapPool Pool { get; }

        public ChunkMesher(HeapPool pool)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));


            descs = new BlockEliminaryDescription[1025];
            anims = new TextureAnimation[descs.Length];
            meshProviders = new MeshProvider?[descs.Length];

            Random rng = new Random(1234);
            for (int i = 1; i < descs.Length; i++)
            {
                descs[i] = new(CubeFaces.All, BlockVisualFeatures.CullableSides);
                meshProviders[i] = new CubeMeshProvider() { anims = anims };

                int stepCount = i < 1016 ? 8 : 2;
                var type = i % 2 == 0 ? TextureAnimationType.MixStep : TextureAnimationType.Step;
                anims[i] = TextureAnimation.Create(type, stepCount, (rng.NextSingle() + 1) * 2f);
            }

            //TextureAnimation[] anims = new TextureAnimation[]
            //{
            //default,
            //TextureAnimation.Create(TextureAnimationType.Step, 3, 1f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1f),
            //TextureAnimation.Create(TextureAnimationType.Step, 2, 1f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1f),
            //TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
            //TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
            //};
        }

        [SkipLocalsInit]
        public unsafe ChunkMeshResult Mesh(
            Chunk chunk,
            Chunk? frontChunk,
            Chunk? backChunk,
            Chunk? topChunk,
            Chunk? bottomChunk,
            Chunk? leftChunk,
            Chunk? rightChunk)
        {
            int storePrepareCapacity = 1024;
            var indexStore = new ByteStore<uint>(Pool, storePrepareCapacity * 6);
            var spaceVertexStore = new ByteStore<ChunkSpaceVertex>(Pool, storePrepareCapacity * 4);
            var paintVertexStore = new ByteStore<ChunkPaintVertex>(Pool, storePrepareCapacity * 4);

            try
            {
                MeshState meshState = new(
                    ref indexStore,
                    ref spaceVertexStore,
                    ref paintVertexStore,
                    0);

                uint* centerRow = stackalloc uint[Chunk.Width + 2];
                uint* bottomRow = stackalloc uint[Chunk.Width];
                uint* topRow = stackalloc uint[Chunk.Width];
                uint* frontRow = stackalloc uint[Chunk.Width];
                uint* backRow = stackalloc uint[Chunk.Width];
                Span<uint> centerSpan = new(centerRow + 1, Chunk.Width);
                Span<uint> bottomSpan = new(bottomRow, Chunk.Width);
                Span<uint> topSpan = new(topRow, Chunk.Width);
                Span<uint> frontSpan = new(frontRow, Chunk.Width);
                Span<uint> backSpan = new(backRow, Chunk.Width);

                void GetBlockRow(int y, int z, Span<uint> destination)
                {
                    if (y == -1)
                    {
                        if (bottomChunk == null)
                            goto Clear;
                        bottomChunk.GetBlockRow(Chunk.Height - 1, z, destination);
                    }
                    else if (y == Chunk.Height)
                    {
                        if (topChunk == null)
                            goto Clear;
                        topChunk.GetBlockRow(0, z, destination);
                    }
                    else if (z == -1)
                    {
                        if (backChunk == null)
                            goto Clear;
                        backChunk.GetBlockRow(y, Chunk.Depth - 1, destination);
                    }
                    else if (z == Chunk.Depth)
                    {
                        if (frontChunk == null)
                            goto Clear;
                        frontChunk.GetBlockRow(y, 0, destination);
                    }
                    else
                    {
                        chunk.GetBlockRow(y, z, destination);
                    }
                    return;

                    Clear:
                    destination.Clear();
                }

                for (int y = 0; y < Chunk.Height; y++)
                {
                    GetBlockRow(y, -1, backSpan);
                    GetBlockRow(y, 0, centerSpan);
                    centerRow[0] = 0;
                    centerRow[Chunk.Width + 1] = 0;

                    for (int z = 0; z < Chunk.Depth; z++)
                    {
                        if (leftChunk != null)
                            centerRow[0] = leftChunk.GetBlock(Chunk.Width - 1, y, z);
                        if (rightChunk != null)
                            centerRow[Chunk.Width + 1] = rightChunk.GetBlock(0, y, z);

                        GetBlockRow(y - 1, z, bottomSpan);
                        GetBlockRow(y + 1, z, topSpan);
                        GetBlockRow(y, z + 1, frontSpan);

                        AppendBody(
                            ref meshState, y, z,
                            descs, meshProviders,
                            centerRow + 1, bottomRow, topRow, frontRow, backRow);

                        // Copy back existing data to reduce getting it from the chunk.
                        centerSpan.CopyTo(backSpan);
                        frontSpan.CopyTo(centerSpan);
                    }
                }

                return ChunkMeshResult.CreateCopyFrom(Pool, indexStore, spaceVertexStore, paintVertexStore);
            }
            finally
            {
                indexStore.Dispose();
                spaceVertexStore.Dispose();
                paintVertexStore.Dispose();
            }
        }

        private unsafe void AppendBody(
            ref MeshState state,
            float y,
            float z,
            BlockEliminaryDescription[] eliminaryDescriptions,
            MeshProvider?[] meshProviders,
            uint* centerRow,
            uint* bottomRow,
            uint* topRow,
            uint* frontRow,
            uint* backRow)
        {
            uint* centerRowL = centerRow - 1;
            uint* centerRowR = centerRow + 1;

            ref BlockEliminaryDescription descs = ref eliminaryDescriptions[0];

            for (int x = 0; x < Chunk.Width; x++)
            {
                uint centerId = centerRow[x];

                MeshProvider? meshProvider = meshProviders[centerId];
                if (meshProvider == null)
                    continue;

                BlockVisualFeatures features = Unsafe.Add(ref descs, (int)centerId).Features;

                if ((features & BlockVisualFeatures.CullableAny) != 0)
                {
                    uint leftId = centerRowL[x];
                    uint rightId = centerRowR[x];
                    uint bottomId = bottomRow[x];
                    uint topId = topRow[x];
                    uint frontId = frontRow[x];
                    uint backId = backRow[x];

                    CubeFaces faces = CubeFaces.All;
                    faces &= ~(Unsafe.Add(ref descs, (int)leftId).OppositeBlockingFaces & CubeFaces.Left);
                    faces &= ~(Unsafe.Add(ref descs, (int)rightId).OppositeBlockingFaces & CubeFaces.Right);
                    faces &= ~(Unsafe.Add(ref descs, (int)bottomId).OppositeBlockingFaces & CubeFaces.Bottom);
                    faces &= ~(Unsafe.Add(ref descs, (int)topId).OppositeBlockingFaces & CubeFaces.Top);
                    faces &= ~(Unsafe.Add(ref descs, (int)frontId).OppositeBlockingFaces & CubeFaces.Front);
                    faces &= ~(Unsafe.Add(ref descs, (int)backId).OppositeBlockingFaces & CubeFaces.Back);

                    var cullableProvider = Unsafe.As<CullableMeshProvider>(meshProvider);
                    if (!cullableProvider.IsEmpty(faces))
                    {
                        Vector3 position = new(x, y, z);
                        cullableProvider.Provide(ref state, centerId, position, faces);
                    }
                }
                else
                {
                    Vector3 position = new(x, y, z);
                    meshProvider.Provide(ref state, centerId, position);
                }
            }
        }
    }
}