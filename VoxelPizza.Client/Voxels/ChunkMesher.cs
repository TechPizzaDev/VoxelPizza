using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    public partial class ChunkMesher
    {
        private BlockVisualFeatures[] visualFeatures;
        private CubeFaces[] oppositeBlockingFaces;
        private TextureAnimation[] anims;
        private MeshProvider?[] meshProviders;

        public HeapPool Pool { get; }

        public unsafe ChunkMesher(HeapPool pool)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));

            visualFeatures = new BlockVisualFeatures[1025];
            oppositeBlockingFaces = new CubeFaces[visualFeatures.Length];
            anims = new TextureAnimation[visualFeatures.Length];
            meshProviders = new MeshProvider?[visualFeatures.Length];

            Random rng = new Random(1234);
            for (int i = 1; i < visualFeatures.Length; i++)
            {
                visualFeatures[i] = BlockVisualFeatures.FaceDependent | BlockVisualFeatures.SkipIfObstructed;
                oppositeBlockingFaces[i] = CubeFaces.All.Opposite();
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

        private static void GetBlockRow(
            nint y,
            nint z,
            ref uint destination,
            Chunk chunk,
            Chunk? frontChunk,
            Chunk? backChunk,
            Chunk? topChunk,
            Chunk? bottomChunk)
        {
            if (y == -1)
            {
                if (bottomChunk == null)
                    goto Clear;
                bottomChunk.GetBlockRowUnsafe(Chunk.Height - 1, z, ref destination);
            }
            else if (y == Chunk.Height)
            {
                if (topChunk == null)
                    goto Clear;
                topChunk.GetBlockRowUnsafe(0, z, ref destination);
            }
            else if (z == -1)
            {
                if (backChunk == null)
                    goto Clear;
                backChunk.GetBlockRowUnsafe(y, Chunk.Depth - 1, ref destination);
            }
            else if (z == Chunk.Depth)
            {
                if (frontChunk == null)
                    goto Clear;
                frontChunk.GetBlockRowUnsafe(y, 0, ref destination);
            }
            else
            {
                chunk.GetBlockRowUnsafe(y, z, ref destination);
            }
            return;

            Clear:
            Unsafe.InitBlockUnaligned(
                startAddress: ref Unsafe.As<uint, byte>(ref destination),
                value: 0,
                byteCount: sizeof(uint) * Chunk.Width);
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
            // TODO: chunk/draw layers (seperate mesh provider arrays per layer)?
            //       e.g. this could allow for vertices (including custom) for a "gas" or "fluid" layer 

            int storePrepareCapacity = 1024;
            var indexStore = new ByteStore<uint>(Pool, storePrepareCapacity * 6);
            var spaceVertexStore = new ByteStore<ChunkSpaceVertex>(Pool, storePrepareCapacity * 4);
            var paintVertexStore = new ByteStore<ChunkPaintVertex>(Pool, storePrepareCapacity * 4);

            try
            {
                ChunkMeshOutput meshOutput = new(
                    ref indexStore,
                    ref spaceVertexStore,
                    ref paintVertexStore,
                    0);

                //uint maxBlockIdExclusive = (uint)meshProviders.Length;

                const int rowBufferLength = (Chunk.Width + 2) * 5;
                uint* rowBuffer = stackalloc uint[rowBufferLength];

                Span<uint> leftSideRowBuffer = stackalloc uint[Chunk.Depth];
                if (leftChunk == null)
                    leftSideRowBuffer.Clear();

                Span<uint> rightSideRowBuffer = stackalloc uint[Chunk.Depth];
                if (rightChunk == null)
                    rightSideRowBuffer.Clear();

                ChunkMesherState mesherState = new(
                    visualFeatures,
                    oppositeBlockingFaces,
                    meshProviders,
                    new Span<uint>(rowBuffer, rowBufferLength));

                ref uint centerSideLeft = ref mesherState.CenterRowL;
                ref uint centerSideRight = ref Unsafe.Add(ref centerSideLeft, Chunk.Width + 1);
                ref uint leftSideRow = ref MemoryMarshal.GetReference(leftSideRowBuffer);
                ref uint rightSideRow = ref MemoryMarshal.GetReference(rightSideRowBuffer);

                ref uint centerRow = ref mesherState.CenterRow;
                ref uint bottomRow = ref mesherState.BottomRow;
                ref uint topRow = ref mesherState.TopRow;
                ref uint frontRow = ref mesherState.FrontRow;
                ref uint backRow = ref mesherState.BackRow;

                for (nint y = 0; y < Chunk.Height; y++)
                {
                    GetBlockRow(y, -1, ref backRow, chunk, frontChunk, backChunk, topChunk, bottomChunk);
                    GetBlockRow(y, 0, ref centerRow, chunk, frontChunk, backChunk, topChunk, bottomChunk);

                    if (leftChunk != null)
                        leftChunk.GetBlockSideRowUnsafe(y, Chunk.Width - 1, ref leftSideRow);

                    if (rightChunk != null)
                        rightChunk.GetBlockSideRowUnsafe(0, y, ref rightSideRow);

                    for (nint z = 0; z < Chunk.Depth; z++)
                    {
                        centerSideLeft = Unsafe.Add(ref leftSideRow, z);
                        centerSideRight = Unsafe.Add(ref rightSideRow, z);

                        GetBlockRow(y - 1, z, ref bottomRow, chunk, frontChunk, backChunk, topChunk, bottomChunk);
                        GetBlockRow(y + 1, z, ref topRow, chunk, frontChunk, backChunk, topChunk, bottomChunk);
                        GetBlockRow(y, z + 1, ref frontRow, chunk, frontChunk, backChunk, topChunk, bottomChunk);

                        mesherState.Y = y;
                        mesherState.Z = z;

                        MeshRow(ref meshOutput, ref mesherState);

                        // Swap loaded rows to reduce access to chunks.
                        Unsafe.CopyBlockUnaligned(
                            destination: ref Unsafe.As<uint, byte>(ref backRow),
                            source: ref Unsafe.As<uint, byte>(ref centerRow),
                            byteCount: sizeof(uint) * Chunk.Width);

                        Unsafe.CopyBlockUnaligned(
                            destination: ref Unsafe.As<uint, byte>(ref centerRow),
                            source: ref Unsafe.As<uint, byte>(ref frontRow),
                            byteCount: sizeof(uint) * Chunk.Width);
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

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void MeshRow(
            ref ChunkMeshOutput meshOutput,
            ref ChunkMesherState mesherState)
        {
            ref MeshProvider? meshProviders = ref MemoryMarshal.GetReference(mesherState.MeshProviders);
            ref BlockVisualFeatures visualFeatures = ref MemoryMarshal.GetReference(mesherState.VisualFeatures);
            ref CubeFaces oppositeBlockingFaces = ref MemoryMarshal.GetReference(mesherState.OppositeBlockingFaces);

            ref uint coreRow = ref mesherState.CenterRow;
            ref uint coreRowL = ref mesherState.CenterRowL;
            ref uint coreRowR = ref mesherState.CenterRowR;
            ref uint bottomRow = ref mesherState.BottomRow;
            ref uint topRow = ref mesherState.TopRow;
            ref uint frontRow = ref mesherState.FrontRow;
            ref uint backRow = ref mesherState.BackRow;

            for (nint x = 0; x < Chunk.Width; x++)
            {
                nint coreId = (nint)Unsafe.Add(ref coreRow, x);

                MeshProvider? meshProvider = Unsafe.Add(ref meshProviders, coreId);
                if (meshProvider == null)
                {
                    continue;
                }

                uint features = (uint)Unsafe.Add(ref visualFeatures, coreId);

                if ((features & (uint)BlockVisualFeatures.FaceDependent) == (uint)BlockVisualFeatures.FaceDependent)
                {
                    uint faces = (uint)CubeFaces.All;
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref coreRowL, x)) & CubeFaces.Left);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref coreRowR, x)) & CubeFaces.Right);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref bottomRow, x)) & CubeFaces.Bottom);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref topRow, x)) & CubeFaces.Top);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref frontRow, x)) & CubeFaces.Front);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, (nint)Unsafe.Add(ref backRow, x)) & CubeFaces.Back);

                    if ((features & (uint)BlockVisualFeatures.SkipIfObstructed) == (uint)BlockVisualFeatures.SkipIfObstructed)
                    {
                        if (faces == (uint)CubeFaces.None)
                        {
                            continue;
                        }
                    }

                    mesherState.X = x;

                    Unsafe.As<FaceDependentMeshProvider>(meshProvider).GenerateFull(
                        ref meshOutput,
                        ref mesherState,
                        (CubeFaces)faces);

                    continue;
                }

                mesherState.X = x;

                meshProvider.GenerateFull(
                    ref meshOutput,
                    ref mesherState);
            }
        }
    }
}