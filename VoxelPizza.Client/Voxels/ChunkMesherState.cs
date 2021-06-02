using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    public unsafe ref struct ChunkMesherState
    {
        public readonly ReadOnlySpan<BlockVisualFeatures> VisualFeatures;
        public readonly ReadOnlySpan<CubeFaces> OppositeBlockingFaces;
        public readonly ReadOnlySpan<MeshProvider?> MeshProviders;
        public readonly ReadOnlySpan<uint> RowBuffer;

        public nint X;
        public nint Y;
        public nint Z;

        public readonly ref uint CenterRowL => ref GetRowL(0);
        public readonly ref uint CenterRow => ref Unsafe.Add(ref CenterRowL, 1);
        public readonly ref uint CenterRowR => ref Unsafe.Add(ref CenterRowL, 2);

        public readonly ref uint BottomRowL => ref GetRowL(1);
        public readonly ref uint BottomRow => ref Unsafe.Add(ref BottomRowL, 1);
        public readonly ref uint BottomRowR => ref Unsafe.Add(ref BottomRowL, 2);

        public readonly ref uint TopRowL => ref GetRowL(2);
        public readonly ref uint TopRow => ref Unsafe.Add(ref TopRowL, 1);
        public readonly ref uint TopRowR => ref Unsafe.Add(ref TopRowL, 2);

        public readonly ref uint FrontRowL => ref GetRowL(3);
        public readonly ref uint FrontRow => ref Unsafe.Add(ref FrontRowL, 1);
        public readonly ref uint FrontRowR => ref Unsafe.Add(ref FrontRowL, 2);

        public readonly ref uint BackRowL => ref GetRowL(4);
        public readonly ref uint BackRow => ref Unsafe.Add(ref BackRowL, 1);
        public readonly ref uint BackRowR => ref Unsafe.Add(ref BackRowL, 2);

        public readonly ref uint TopFrontRowL => ref GetRowL(5);
        public readonly ref uint TopFrontRow => ref Unsafe.Add(ref TopFrontRowL, 1);
        public readonly ref uint TopFrontRowR => ref Unsafe.Add(ref TopFrontRowL, 2);

        public readonly uint CenterId => Unsafe.Add(ref CenterRow, X);
        public readonly uint CenterLeftId => Unsafe.Add(ref CenterRowL, X);
        public readonly uint CenterRightId => Unsafe.Add(ref CenterRowR, X);

        public readonly uint BottomId => Unsafe.Add(ref BottomRow, X);
        public readonly uint TopId => Unsafe.Add(ref TopRow, X);
        public readonly uint FrontId => Unsafe.Add(ref FrontRow, X);
        public readonly uint BackId => Unsafe.Add(ref BackRow, X);

        public ChunkMesherState(
            ReadOnlySpan<BlockVisualFeatures> visualFeatures,
            ReadOnlySpan<CubeFaces> oppositeBlockingFaces,
            ReadOnlySpan<MeshProvider?> meshProviders,
            ReadOnlySpan<uint> rowBuffer)
        {
            VisualFeatures = visualFeatures;
            OppositeBlockingFaces = oppositeBlockingFaces;
            MeshProviders = meshProviders;
            RowBuffer = rowBuffer;

            X = 0;
            Y = 0;
            Z = 0;
        }

        private readonly ref uint GetRowL(nint index)
        {
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(RowBuffer), (Chunk.Width + 2) * index);
        }
    }
}