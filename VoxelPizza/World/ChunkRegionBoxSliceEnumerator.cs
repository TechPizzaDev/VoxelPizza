using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public struct ChunkRegionBoxSliceEnumerator : IEnumerator<ChunkRegionBoxSlice>
    {
        private int processedY;
        private int chunkY;
        private int regionY;
        private int innerY;
        private int height;

        private int processedZ;
        private int chunkZ;
        private int regionZ;
        private int innerZ;
        private int depth;

        private int processedX;
        private int chunkX;
        private int regionX;
        private int width;

        public readonly ChunkPosition Origin;
        public readonly Size3 Size;
        public readonly ChunkPosition Max;

        public readonly ChunkRegionPosition CurrentRegion => new(regionX, regionY, regionZ);

        public readonly ChunkPosition CurrentInnerOrigin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
                (int)((uint)chunkX % ChunkRegion.Width),
                innerY,
                innerZ);
        }

        public readonly ChunkPosition CurrentOuterOrigin
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(
                chunkX - Origin.X,
                chunkY - Origin.Y,
                chunkZ - Origin.Z);
        }

        public readonly Size3 CurrentSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new((uint)width, (uint)height, (uint)depth);
        }

        public readonly ChunkRegionBoxSlice Current
        {
            get
            {
                ChunkRegionPosition region = CurrentRegion;
                ChunkPosition outerOrigin = CurrentOuterOrigin;
                ChunkPosition innerOrigin = CurrentInnerOrigin;
                Size3 size = CurrentSize;
                return new ChunkRegionBoxSlice(region, outerOrigin, innerOrigin, size);
            }
        }

        readonly object IEnumerator.Current => Current;

        public ChunkRegionBoxSliceEnumerator(ChunkPosition origin, ChunkPosition max) : this()
        {
            Debug.Assert(origin.X <= max.X && origin.Y <= max.Y && origin.Z <= max.Z);

            Origin = origin;
            Max = max;

            ChunkPosition size = max - origin;
            Size = new Size3((uint)size.X, (uint)size.Y, (uint)size.Z);

            UpdateY();
            UpdateZ();
        }

        public readonly ChunkRegionBoxSliceEnumerator GetEnumerator()
        {
            return this;
        }

        private void UpdateY()
        {
            chunkY = Origin.Y + processedY;
            regionY = ChunkRegion.ChunkToRegionY(chunkY);
            innerY = (int)((uint)chunkY % ChunkRegion.Height);

            int min1Y = regionY * ChunkRegion.Height;
            int max1Y = min1Y + ChunkRegion.Height;
            int bottomSide = Math.Max(min1Y, Origin.Y);
            int topSide = Math.Min(max1Y, Max.Y);
            height = topSide - bottomSide;
        }

        private void UpdateZ()
        {
            chunkZ = Origin.Z + processedZ;
            regionZ = ChunkRegion.ChunkToRegionZ(chunkZ);
            innerZ = (int)((uint)chunkZ % ChunkRegion.Depth);

            int min1Z = regionZ * ChunkRegion.Depth;
            int max1Z = min1Z + ChunkRegion.Depth;
            int backSide = Math.Max(min1Z, Origin.Z);
            int frontSide = Math.Min(max1Z, Max.Z);
            depth = frontSide - backSide;
        }

        public bool MoveNext()
        {
            if (processedX < Size.W)
            {
                chunkX = Origin.X + processedX;
                regionX = ChunkRegion.ChunkToRegionX(chunkX);

                int min1X = regionX * ChunkRegion.Width;
                int max1X = min1X + ChunkRegion.Width;
                int leftSide = Math.Max(min1X, Origin.X);
                int rightSide = Math.Min(max1X, Max.X);
                width = rightSide - leftSide;

                processedX += width;

                return true;
            }

            return MoveNextZY();
        }

        private bool MoveNextZY()
        {
            processedX = 0;
            // X will be updated in the next call

            processedZ += depth;
            UpdateZ();

            if (processedZ < Size.D)
            {
                return MoveNext();
            }

            processedZ = 0;
            UpdateZ();

            processedY += height;
            UpdateY();

            if (processedY < Size.H)
            {
                return MoveNext();
            }

            return false;
        }

        public void Reset()
        {
            processedY = 0;
            UpdateY();

            processedZ = 0;
            UpdateZ();

            processedX = 0;
            chunkX = 0;
            regionX = 0;
            width = 0;
        }

        public void Dispose()
        {
        }
    }
}
