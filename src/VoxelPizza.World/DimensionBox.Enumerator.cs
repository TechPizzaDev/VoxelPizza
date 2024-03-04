using System;
using System.Diagnostics;
using VoxelPizza.Numerics;

namespace VoxelPizza.World;

public readonly partial struct DimensionBox
{
    public struct Enumerator
    {
        private const int Width = Chunk.Width * ChunkRegion.Width;
        private const int Height = Chunk.Height * ChunkRegion.Height;
        private const int Depth = Chunk.Depth * ChunkRegion.Depth;

        private int processedY;
        private int blockY;
        private int regionY;
        private int innerY;
        private int height;

        private int processedZ;
        private int blockZ;
        private int regionZ;
        private int innerZ;
        private int depth;

        private int processedX;
        private int blockX;
        private int regionX;
        private int width;

        public readonly BlockPosition Origin;
        public readonly Size3 Size;
        public readonly BlockPosition Max;

        public readonly ChunkRegionPosition CurrentRegion => new(regionX, regionY, regionZ);

        public readonly BlockPosition CurrentInnerOrigin
        {
            get => new(
                (int)((uint)blockX % Width),
                innerY,
                innerZ);
        }

        public readonly BlockPosition CurrentBlock => new(blockX, blockY, blockZ);
    
        public readonly BlockPosition CurrentOuterOrigin => CurrentBlock - Origin;
    
        public readonly Size3 CurrentSize
        {
            get
            {
                Debug.Assert(width >= 0 && height >= 0 && depth >= 0);
                return new Size3((uint)width, (uint)height, (uint)depth);
            }
        }

        public readonly DimensionBoxSlice Current
        {
            get
            {
                ChunkRegionPosition region = CurrentRegion;
                BlockPosition outerOrigin = CurrentOuterOrigin;
                BlockPosition innerOrigin = CurrentInnerOrigin;
                Size3 size = CurrentSize;
                return new DimensionBoxSlice(region, outerOrigin, innerOrigin, size);
            }
        }

        public Enumerator(BlockPosition origin, BlockPosition max) : this()
        {
            Origin = origin;
            Max = max;

            BlockPosition size = max - origin;
            Size = new Size3((uint)size.X, (uint)size.Y, (uint)size.Z);

            UpdateY();
            UpdateZ();
        }
        
        public readonly Enumerator GetEnumerator()
        {
            return this;
        }

        public readonly int GetMaxCount()
        {
            uint w = Size.W / Width + 2;
            uint h = Size.H / Height + 2;
            uint d = Size.D / Depth + 2;
            return (int)(w * h * d);
        }

        private void UpdateY()
        {
            blockY = Origin.Y + processedY;
            regionY = ChunkRegion.ChunkToRegionY(Chunk.BlockToChunkY(blockY));
            innerY = (int)((uint)blockY % Height);

            int min1Y = regionY * Height;
            int max1Y = min1Y + Height;
            int bottomSide = Math.Max(min1Y, Origin.Y);
            int topSide = Math.Min(max1Y, Max.Y);
            height = topSide - bottomSide;
        }

        private void UpdateZ()
        {
            blockZ = Origin.Z + processedZ;
            regionZ = ChunkRegion.ChunkToRegionZ(Chunk.BlockToChunkZ(blockZ));
            innerZ = (int)((uint)blockZ % Depth);

            int min1Z = regionZ * Depth;
            int max1Z = min1Z + Depth;
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
                regionX = ChunkRegion.ChunkToRegionX(Chunk.BlockToChunkX(blockX));

                int min1X = regionX * Width;
                int max1X = min1X + Width;
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
            regionX = 0;
            width = 0;
        }
    }
}