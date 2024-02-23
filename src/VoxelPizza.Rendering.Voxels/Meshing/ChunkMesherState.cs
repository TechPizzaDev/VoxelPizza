using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe ref struct ChunkMesherState
    {
        public readonly ReadOnlySpan<BlockVisualFeatures> VisualFeatures;
        public readonly ReadOnlySpan<CubeFaces> OppositeBlockingFaces;
        public readonly ReadOnlySpan<MeshProvider?> MeshProviders;

        public readonly ReadOnlySpan<uint> Data;
        public readonly nuint RowStride;
        public readonly nuint LayerStride;
        public readonly nuint InnerSizeW;
        public readonly nuint InnerSizeH;
        public readonly nuint InnerSizeD;

        public nuint Index;
        public nuint X;
        public nuint Y;
        public nuint Z;

        public readonly ref uint CoreRow => ref Unsafe.Add(ref MemoryMarshal.GetReference(Data), Index);
        public readonly ref uint TopRow => ref Unsafe.Add(ref MemoryMarshal.GetReference(Data), Index + LayerStride);
        public readonly ref uint BottomRow => ref Unsafe.Add(ref MemoryMarshal.GetReference(Data), Index - LayerStride);
        public readonly ref uint FrontRow => ref Unsafe.Add(ref MemoryMarshal.GetReference(Data), Index + RowStride);
        public readonly ref uint BackRow => ref Unsafe.Add(ref MemoryMarshal.GetReference(Data), Index - RowStride);

        public readonly uint CoreId => Unsafe.Add(ref CoreRow, X);

        public ChunkMesherState(
            ReadOnlySpan<BlockVisualFeatures> visualFeatures,
            ReadOnlySpan<CubeFaces> oppositeBlockingFaces,
            ReadOnlySpan<MeshProvider?> meshProviders,
            ReadOnlySpan<uint> data,
            nuint rowStride,
            nuint layerStride,
            Size3 innerSize)
        {
            VisualFeatures = visualFeatures;
            OppositeBlockingFaces = oppositeBlockingFaces;
            MeshProviders = meshProviders;

            Data = data;
            RowStride = rowStride;
            LayerStride = layerStride;
            InnerSizeW = innerSize.W;
            InnerSizeH = innerSize.H;
            InnerSizeD = innerSize.D;

            Index = 0;
            X = 0;
            Y = 0;
            Z = 0;
        }
    }
}