using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public class ChunkMesher : IDisposable
    {
        private BlockVisualFeatures[] visualFeatures;
        private CubeFaces[] oppositeBlockingFaces;
        private TextureAnimation[] anims;
        private MeshProvider?[] meshProviders;

        private ByteStore<uint> _indexStore;
        private ByteStore<ChunkSpaceVertex> _spaceVertexStore;
        private ByteStore<ChunkPaintVertex> _paintVertexStore;

        public MemoryHeap Heap { get; }

        public unsafe ChunkMesher(MemoryHeap heap)
        {
            Heap = heap ?? throw new ArgumentNullException(nameof(heap));

            visualFeatures = new BlockVisualFeatures[1025];
            oppositeBlockingFaces = new CubeFaces[visualFeatures.Length];
            anims = new TextureAnimation[visualFeatures.Length];
            meshProviders = new MeshProvider?[visualFeatures.Length];

            Random rng = new(1234);
            for (int i = 1; i < visualFeatures.Length; i++)
            {
                visualFeatures[i] = BlockVisualFeatures.FaceDependent | BlockVisualFeatures.SkipIfObstructed;
                oppositeBlockingFaces[i] = CubeFaces.All.Opposite();
                meshProviders[i] = new CubeMeshProvider() { anims = anims };

                int stepCount = i < 1016 ? 8 : 2;
                TextureAnimationType type = i % 2 == 0 ? TextureAnimationType.MixStep : TextureAnimationType.Step;
                anims[i] = TextureAnimation.Create(type, stepCount, (rng.NextSingle() + 1) * 2f);
            }

            _indexStore = new(Heap);
            _spaceVertexStore = new(Heap);
            _paintVertexStore = new(Heap);

            uint storePrepareCapacity = 1024 * 4;
            _indexStore.PrepareCapacityFor(storePrepareCapacity * 6);
            _spaceVertexStore.PrepareCapacityFor(storePrepareCapacity * 4);
            _paintVertexStore.PrepareCapacityFor(storePrepareCapacity * 4);
        }

        [SkipLocalsInit]
        public unsafe ChunkMeshResult Mesh(BlockMemory worldSlice)
        {
            // TODO: chunk/draw layers (seperate mesh provider arrays per layer)?
            //       e.g. this could allow for vertices (including custom) for a "gas" or "fluid" layer 

            _indexStore.Clear();
            _spaceVertexStore.Clear();
            _paintVertexStore.Clear();

            {
                ChunkMeshOutput meshOutput = new(
                    ref _indexStore,
                    ref _spaceVertexStore,
                    ref _paintVertexStore,
                    0);

                Size3 outerSize = worldSlice.OuterSize;
                Size3 innerSize = worldSlice.InnerSize;

                nuint xOffset = (outerSize.W - innerSize.W) / 2;
                nuint yOffset = (outerSize.H - innerSize.H) / 2;
                nuint zOffset = (outerSize.D - innerSize.D) / 2;

                nuint depth = outerSize.D;
                nuint rowStride = outerSize.W;
                nuint layerStride = rowStride * depth;

                ChunkMesherState mesherState = new(
                    visualFeatures,
                    oppositeBlockingFaces,
                    meshProviders,
                    ref MemoryMarshal.GetReference(worldSlice.Data.AsSpan()),
                    rowStride,
                    layerStride,
                    innerSize);

                for (nuint y = 0; y < mesherState.InnerSizeH; y++)
                {
                    mesherState.Y = y;

                    for (nuint z = 0; z < mesherState.InnerSizeD; z++)
                    {
                        mesherState.Z = z;

                        mesherState.Index = Chunk.GetIndexBase(
                            outerSize.D,
                            rowStride,
                            yOffset + y,
                            zOffset + z)
                            + xOffset;

                        MeshRow(ref meshOutput, ref mesherState);
                    }
                }

                return ChunkMeshResult.CreateCopyFrom(
                    Heap,
                    _indexStore,
                    _spaceVertexStore,
                    _paintVertexStore);
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

            ref uint coreRow = ref mesherState.CoreRow;
            ref uint coreRowL = ref Unsafe.Add(ref coreRow, -1);
            ref uint coreRowR = ref Unsafe.Add(ref coreRow, 1);
            ref uint bottomRow = ref mesherState.BottomRow;
            ref uint topRow = ref mesherState.TopRow;
            ref uint frontRow = ref mesherState.FrontRow;
            ref uint backRow = ref mesherState.BackRow;

            for (nuint x = 0; x < mesherState.InnerSizeW; x++)
            {
                nuint coreId = Unsafe.Add(ref coreRow, x);

                MeshProvider? meshProvider = Unsafe.Add(ref meshProviders, coreId);
                if (meshProvider == null)
                    continue;

                uint features = (uint)Unsafe.Add(ref visualFeatures, coreId);

                if ((features & (uint)BlockVisualFeatures.FaceDependent) == (uint)BlockVisualFeatures.FaceDependent)
                {
                    uint faces = (uint)CubeFaces.All;
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref coreRowL, x)) & CubeFaces.Left);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref coreRowR, x)) & CubeFaces.Right);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref bottomRow, x)) & CubeFaces.Bottom);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref topRow, x)) & CubeFaces.Top);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref frontRow, x)) & CubeFaces.Front);
                    faces &= ~(uint)(Unsafe.Add(ref oppositeBlockingFaces, Unsafe.Add(ref backRow, x)) & CubeFaces.Back);

                    if ((features & (uint)BlockVisualFeatures.SkipIfObstructed) == (uint)BlockVisualFeatures.SkipIfObstructed)
                    {
                        if (faces == (uint)CubeFaces.None)
                            continue;
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

        public void Dispose()
        {
            _indexStore.Dispose();
            _spaceVertexStore.Dispose();
            _paintVertexStore.Dispose();
        }
    }
} 