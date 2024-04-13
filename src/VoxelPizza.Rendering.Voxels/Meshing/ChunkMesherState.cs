using System;
using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Rendering.Voxels.Meshing
{
    public unsafe ref struct ChunkMesherState
    {
        public readonly ReadOnlySpan<BlockVisualFeatures> VisualFeatures;
        public readonly ReadOnlySpan<CubeFaces> OppositeBlockingFaces;
        public readonly ReadOnlySpan<MeshProvider?> MeshProviders;

        public readonly ref uint Data;
        public readonly uint RowStride;
        public readonly uint LayerStride;
        public readonly uint InnerSizeW;
        public readonly uint InnerSizeH;
        public readonly uint InnerSizeD;

        public uint Index;
        public uint X;
        public uint Y;
        public uint Z;

        public readonly ref uint CoreRow => ref Unsafe.Add(ref Data, Index);
        public readonly ref uint TopRow => ref Unsafe.Add(ref Data, Index + LayerStride);
        public readonly ref uint BottomRow => ref Unsafe.Add(ref Data, Index - LayerStride);
        public readonly ref uint FrontRow => ref Unsafe.Add(ref Data, Index + RowStride);
        public readonly ref uint BackRow => ref Unsafe.Add(ref Data, Index - RowStride);

        public readonly uint CoreId => Unsafe.Add(ref CoreRow, X);

        public ChunkMesherState(
            ReadOnlySpan<BlockVisualFeatures> visualFeatures,
            ReadOnlySpan<CubeFaces> oppositeBlockingFaces,
            ReadOnlySpan<MeshProvider?> meshProviders,
            ref uint data,
            uint rowStride,
            uint layerStride,
            Size3 innerSize)
        {
            VisualFeatures = visualFeatures;
            OppositeBlockingFaces = oppositeBlockingFaces;
            MeshProviders = meshProviders;

            Data = ref data;
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