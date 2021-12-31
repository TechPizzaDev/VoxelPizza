using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public struct ChunkBoxSliceEnumerator : IEnumerator<ChunkBoxSlice>
    {
        private int processedY;
        private int blockY;
        private int chunkY;
        private int innerY;
        private int height;

        private int processedZ;
        private int blockZ;
        private int chunkZ;
        private int innerZ;
        private int depth;

        private int processedX;
        private int blockX;
        private int chunkX;
        private int width;

        public BlockPosition Origin { get; }
        public Size3 Size { get; }
        public BlockPosition Max { get; }

        public readonly ChunkPosition CurrentChunk => new(chunkX, chunkY, chunkZ);

        public readonly BlockPosition CurrentInnerOrigin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
                (int)((uint)blockX % Chunk.Width),
                innerY,
                innerZ);
        }

        public readonly BlockPosition CurrentOuterOrigin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
                blockX - Origin.X,
                blockY - Origin.Y,
                blockZ - Origin.Z);
        }

        public readonly Size3 CurrentSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(width >= 0 && height >= 0 && depth >= 0);
                return new Size3((uint)width, (uint)height, (uint)depth);
            }
        }

        public readonly ChunkBoxSlice Current
        {
            get
            {
                ChunkPosition chunk = CurrentChunk;
                BlockPosition outerOrigin = CurrentOuterOrigin;
                BlockPosition innerOrigin = CurrentInnerOrigin;
                Size3 size = CurrentSize;
                return new ChunkBoxSlice(chunk, outerOrigin, innerOrigin, size);
            }
        }

        readonly object IEnumerator.Current => Current;

        public ChunkBoxSliceEnumerator(BlockPosition origin, Size3 size) : this()
        {
            Origin = origin;
            Size = size;
            Max = new WorldBox(origin, size).Max;

            UpdateY();
            UpdateZ();
        }

        public readonly ChunkBoxSliceEnumerator GetEnumerator()
        {
            return this;
        }

        public readonly int GetMaxChunkCount()
        {
            uint w = Size.W / Chunk.Width + 2;
            uint h = Size.H / Chunk.Height + 2;
            uint d = Size.D / Chunk.Depth + 2;
            return (int)(w * h * d);
        }

        private void UpdateY()
        {
            blockY = Origin.Y + processedY;
            chunkY = Chunk.BlockToChunkY(blockY);
            innerY = (int)((uint)blockY % Chunk.Height);

            int min1Y = chunkY * Chunk.Height;
            int max1Y = min1Y + Chunk.Height;
            int bottomSide = Math.Max(min1Y, Origin.Y);
            int topSide = Math.Min(max1Y, Max.Y);
            height = topSide - bottomSide;
        }

        private void UpdateZ()
        {
            blockZ = Origin.Z + processedZ;
            chunkZ = Chunk.BlockToChunkZ(blockZ);
            innerZ = (int)((uint)blockZ % Chunk.Depth);

            int min1Z = chunkZ * Chunk.Depth;
            int max1Z = min1Z + Chunk.Depth;
            int backSide = Math.Max(min1Z, Origin.Z);
            int frontSide = Math.Min(max1Z, Max.Z);
            depth = frontSide - backSide;
        }

        public bool MoveNext()
        {
            while (processedY < Size.H)
            {
                while (processedZ < Size.D)
                {
                    while (processedX < Size.W)
                    {
                        blockX = Origin.X + processedX;
                        chunkX = Chunk.BlockToChunkX(blockX);

                        int min1X = chunkX * Chunk.Width;
                        int max1X = min1X + Chunk.Width;
                        int leftSide = Math.Max(min1X, Origin.X);
                        int rightSide = Math.Min(max1X, Max.X);
                        width = rightSide - leftSide;

                        processedX += width;

                        return true;
                    }

                    processedX = 0;
                    // X will be updated in the next call

                    processedZ += depth;
                    UpdateZ();
                }

                processedZ = 0;
                UpdateZ();

                processedY += height;
                UpdateY();
            }

            return false;
        }

        public void Reset()
        {
            processedY = 0;
            chunkY = 0;
            height = 0;

            processedZ = 0;
            chunkZ = 0;
            depth = 0;

            processedX = 0;
            chunkX = 0;
            width = 0;
        }

        public void Dispose()
        {
        }
    }
}
