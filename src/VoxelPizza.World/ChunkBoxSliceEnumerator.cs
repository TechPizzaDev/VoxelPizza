using System;
using System.Diagnostics;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public struct ChunkBoxSliceEnumerator
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

        public readonly BlockPosition Origin;
        public readonly Size3 Size;
        public readonly BlockPosition Max;

        public readonly ChunkPosition CurrentChunk => new(chunkX, chunkY, chunkZ);

        public readonly BlockPosition CurrentInnerOrigin
        {
            get => new(
                (int)((uint)blockX % Chunk.Width),
                innerY,
                innerZ);
        }

        public readonly BlockPosition CurrentOuterOrigin => CurrentBlock - Origin;
        
        public readonly BlockPosition CurrentBlock => new(blockX, blockY, blockZ);

        public readonly Size3 CurrentSize
        {
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
                BlockPosition block = CurrentBlock;
                BlockPosition innerOrigin = CurrentInnerOrigin;
                Size3 size = CurrentSize;
                return new ChunkBoxSlice(chunk, block, innerOrigin, size);
            }
        }

        public ChunkBoxSliceEnumerator(BlockPosition origin, BlockPosition max) : this()
        {
            Origin = origin;
            Max = max;

            BlockPosition size = max - origin;
            Size = new Size3((uint)size.X, (uint)size.Y, (uint)size.Z);

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
            TryMove:
            if (processedX < Size.W)
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

            if (processedZ < Size.D)
            {
                goto TryMove;
            }

            processedZ = 0;
            UpdateZ();

            processedY += height;
            UpdateY();

            if (processedY < Size.H)
            {
                goto TryMove;
            }

            return false;
        }

        public void Reset()
        {
            processedY = 0;
            UpdateY();

            processedZ = 0;
            UpdateZ();

            blockX = 0;
            processedX = 0;
            chunkX = 0;
            width = 0;
        }
    }
}
