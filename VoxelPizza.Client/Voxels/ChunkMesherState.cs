using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Client
{
    public unsafe ref struct ChunkMesherState
    {
        public readonly ReadOnlySpan<BlockVisualFeatures> VisualFeatures;
        public readonly ReadOnlySpan<CubeFaces> OppositeBlockingFaces;
        public readonly ReadOnlySpan<MeshProvider?> MeshProviders;

        public readonly ReadOnlySpan<uint> Data;
        public readonly nint RowStride;
        public readonly nint LayerStride;
        public readonly nint InnerSizeW;
        public readonly nint InnerSizeH;
        public readonly nint InnerSizeD;

        public nint Index;
        public nint X;
        public nint Y;
        public nint Z;

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
            nint rowStride,
            nint layerStride,
            Size3 innerSize)
        {
            VisualFeatures = visualFeatures;
            OppositeBlockingFaces = oppositeBlockingFaces;
            MeshProviders = meshProviders;

            Data = data;
            RowStride = rowStride;
            LayerStride = layerStride;
            InnerSizeW = (nint)innerSize.W;
            InnerSizeH = (nint)innerSize.H;
            InnerSizeD = (nint)innerSize.D;

            Index = 0;
            X = 0;
            Y = 0;
            Z = 0;
        }
    }
}