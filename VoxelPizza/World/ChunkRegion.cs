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
        private ChunkAction _cachedChunkUpdated;
        private RefCountedAction _cachedChunkRefZeroed;

        public Dimension Dimension { get; }
        public ChunkRegionPosition Position { get; }

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkUpdated;
        public event ChunkAction? ChunkRemoved;

        public ChunkRegion(Dimension dimension, ChunkRegionPosition position)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            Position = position;

            _chunks = new Chunk?[Width * Height * Depth];

            _cachedChunkUpdated = Chunk_ChunkUpdated;
            _cachedChunkRefZeroed = Chunk_RefCountZero;
        }

        private void Chunk_ChunkUpdated(Chunk chunk)
        {
            ChunkUpdated?.Invoke(chunk);
        }

        public Chunk? GetLocalChunk(int index)
        {
            _chunkLock.EnterReadLock();
            try
            {
                Chunk? chunk = _chunks[index];
                chunk?.IncrementRef();
                return chunk;
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
                // GetLocalChunk increments refcount
                return chunk;
            }

            _chunkLock.EnterWriteLock();
            try
            {
                chunk = new Chunk(this, position);
                chunk.Updated += _cachedChunkUpdated;
                chunk.IncrementRef(RefCountType.Container);

                _chunks[index] = chunk;
                ChunkAdded?.Invoke(chunk);
            }
            finally
            {
                _chunkLock.ExitWriteLock();
            }

            chunk.IncrementRef();
            return chunk;
        }

        public bool RemoveChunk(ChunkPosition position)
        {
            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);

            _chunkLock.EnterWriteLock();
            try
            {
                Chunk? chunk = _chunks[index];
                if (chunk == null)
                    return false;

                chunk.RefZeroed += _cachedChunkRefZeroed;
                
                // Invoke event before decrementing ref to let
                // others delay the unload.
                ChunkRemoved?.Invoke(chunk);

                chunk.DecrementRef(RefCountType.Container);

                _chunks[index] = null;
                return true;
            }
            finally
            {
                _chunkLock.ExitWriteLock();
            }
        }

        private void Chunk_RefCountZero(RefCounted instance)
        {
            Chunk chunk = (Chunk)instance;

            chunk.Updated -= _cachedChunkUpdated;
        }

        public static int GetChunkIndex(ChunkPosition localPosition)
        {
            return (localPosition.Y * Depth + localPosition.Z) * Width + localPosition.X;
        }

        public static ChunkPosition GetLocalChunkPosition(ChunkPosition position)
        {
            return new ChunkPosition(
                (int)((uint)position.X % Width),
                (int)((uint)position.Y % Height),
                (int)((uint)position.Z % Depth));
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
