using System;
using System.Runtime.CompilerServices;
using System.Threading;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public class ChunkRegion : RefCounted
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        private Chunk?[] _chunks;
        private ReaderWriterLockSlim _chunkLock = new();

        public Dimension Dimension { get; }
        public ChunkRegionPosition Position { get; }

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkRemoved;

        public ChunkRegion(Dimension dimension, ChunkRegionPosition position)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            Position = position;

            _chunks = new Chunk?[Width * Height * Depth];
        }

        public Chunk? GetLocalChunk(int index)
        {
            _chunkLock.EnterReadLock();
            try
            {
                Chunk? chunk = _chunks[index];
                if (chunk != null)
                {
                    chunk.IncrementRef();
                    return chunk;
                }
                return null;
            }
            finally
            {
                _chunkLock.ExitReadLock();
            }
        }

        public Chunk? GetLocalChunk(ChunkPosition localPosition)
        {
            int index = GetChunkIndex(localPosition);
            return GetLocalChunk(index);
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);
            return GetLocalChunk(localPosition);
        }

        public Chunk CreateChunk(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);

            Chunk? chunk = GetLocalChunk(index);
            if (chunk != null)
            {
                return chunk;
            }

            _chunkLock.EnterWriteLock();
            try
            {
                chunk = new Chunk(this, position);
                _chunks[index] = chunk;
            }
            finally
            {
                _chunkLock.ExitWriteLock();
            }

            chunk.IncrementRef();
            ChunkAdded?.Invoke(chunk);
            return chunk;
        }

        public int GetChunkIndex(ChunkPosition localPosition)
        {
            return (localPosition.Y * (int)Size.D + localPosition.Z) * (int)Size.W + localPosition.X;
        }

        public ChunkPosition GetLocalChunkPosition(ChunkPosition position)
        {
            return new ChunkPosition(
                (int)((uint)position.X % Size.W),
                (int)((uint)position.Y % Size.H),
                (int)((uint)position.Z % Size.D));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkToRegionX(int chunkX)
        {
            return chunkX >> 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkToRegionY(int chunkY)
        {
            return chunkY >> 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkToRegionZ(int chunkZ)
        {
            return chunkZ >> 4;
        }
    }
}
