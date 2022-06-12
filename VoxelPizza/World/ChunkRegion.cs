using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class ChunkRegion : RefCounted
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        private Chunk?[]? _chunks;
        private int _chunkCount;
        private ReaderWriterLockSlim _chunkLock = new();
        private ChunkAction _cachedChunkUpdated;
        private RefCountedAction _cachedChunkRefZeroed;

        public Dimension Dimension { get; }
        public ChunkRegionPosition Position { get; }

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkUpdated;
        public event ChunkAction? ChunkRemoved;
        public event ChunkRegionAction? Empty;

        public bool HasChunks => _chunkCount > 0;

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

        public ChunkBox GetChunkBox()
        {
            return new ChunkBox(Position.ToChunk(), Size);
        }

        public RefCounted<Chunk?> GetLocalChunk(int index)
        {
            _chunkLock.EnterReadLock();
            try
            {
                Chunk?[]? chunks = _chunks;
                if (chunks == null)
                {
                    return default;
                }

                Chunk? chunk = chunks[index];
                return chunk.TrackRef();
            }
            finally
            {
                _chunkLock.ExitReadLock();
            }
        }

        public RefCounted<Chunk?> GetLocalChunk(ChunkPosition localPosition)
        {
            CheckLocalChunkPosition(localPosition);

            int index = GetChunkIndex(localPosition);
            return GetLocalChunk(index);
        }

        public RefCounted<Chunk?> GetChunk(ChunkPosition position)
        {
            CheckChunkPosition(Position, position);

            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);
            return GetLocalChunk(index);
        }

        public RefCounted<Chunk> CreateChunk(ChunkPosition position, out ChunkAddStatus status)
        {
            CheckChunkPosition(Position, position);

            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);

            RefCounted<Chunk?> countedChunk = GetLocalChunk(index);
            if (countedChunk.HasValue)
            {
                // GetLocalChunk increments refcount
                status = ChunkAddStatus.Success;
                return countedChunk!;
            }

            _chunkLock.EnterWriteLock();
            try
            {
                Chunk?[]? chunks = _chunks;
                if (chunks == null)
                {
                    status = ChunkAddStatus.MissingRegion;
                    return default;
                }

                // Check again after acquiring lock,
                // as a chunk may have been created while we were waiting.
                Chunk? chunk = chunks[index];
                if (chunk == null)
                {
                    chunk = new Chunk(this, position);
                    chunk.Updated += _cachedChunkUpdated;
                    chunk.RefZeroed += _cachedChunkRefZeroed;
                    chunk.IncrementRef(RefCountType.Container);

                    chunks[index] = chunk;
                    _chunkCount++;

                    ChunkAdded?.Invoke(chunk);
                }

                status = ChunkAddStatus.Success;
                return chunk.TrackRef();
            }
            finally
            {
                _chunkLock.ExitWriteLock();
            }
        }

        public ChunkRemoveStatus RemoveChunk(ChunkPosition position)
        {
            CheckChunkPosition(Position, position);

            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);

            _chunkLock.EnterWriteLock();
            try
            {
                Chunk?[]? chunks = _chunks;
                if (chunks == null)
                {
                    return ChunkRemoveStatus.MissingRegion;
                }

                Chunk? chunk = chunks[index];
                if (chunk == null)
                {
                    return ChunkRemoveStatus.MissingChunk;
                }

                DecrementChunkRef(chunk);

                chunks[index] = null;
                _chunkCount--;

                if (_chunkCount == 0)
                {
                    Empty?.Invoke(this);
                }

                return ChunkRemoveStatus.Success;
            }
            finally
            {
                _chunkLock.ExitWriteLock();
            }
        }

        private void DecrementChunkRef(Chunk chunk)
        {
            // Invoke event before decrementing ref to let
            // others delay the unload.
            ChunkRemoved?.Invoke(chunk);

            chunk.DecrementRef(RefCountType.Container);
        }

        private void Chunk_RefCountZero(RefCounted instance)
        {
            Chunk chunk = (Chunk)instance;

            chunk.Updated -= _cachedChunkUpdated;
            chunk.RefZeroed -= _cachedChunkRefZeroed;

            chunk.Destroy();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetChunkIndex(ChunkPosition localPosition)
        {
            return (localPosition.Y * Depth + localPosition.Z) * Width + localPosition.X;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return IntMath.DivideRoundDown(chunkX, Width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkToRegionY(int chunkY)
        {
            return IntMath.DivideRoundDown(chunkY, Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ChunkToRegionZ(int chunkZ)
        {
            return IntMath.DivideRoundDown(chunkZ, Depth);
        }

        public static void CheckChunkIndex(int index)
        {
            if (index > Width * Height * Depth)
            {
                throw new IndexOutOfRangeException("The local chunk index is out of range.");
            }
        }

        public static void CheckLocalChunkPosition(ChunkPosition chunk)
        {
            if (chunk.X < 0 || chunk.X >= Size.W ||
                chunk.Y < 0 || chunk.Y >= Size.H ||
                chunk.Z < 0 || chunk.Z >= Size.D)
            {
                throw new IndexOutOfRangeException("The local chunk position is out of range.");
            }
        }

        public static void CheckChunkPosition(ChunkRegionPosition region, ChunkPosition chunk)
        {
            ChunkPosition start = region.ToChunk();
            Int3 end = start.ToInt3() + Size.ToInt3();

            if (chunk.X < start.X || chunk.X >= end.X ||
                chunk.Y < start.Y || chunk.Y >= end.Y ||
                chunk.Z < start.Z || chunk.Z >= end.Z)
            {
                throw new IndexOutOfRangeException("The chunk position is out of range.");
            }
        }

        private string GetDebuggerDisplay()
        {
            return $"{nameof(ChunkRegion)}({Position.ToNumericString()})";
        }

        public void Destroy()
        {
            if (_chunks != null)
            {
                foreach (Chunk? chunk in _chunks)
                {
                    if (chunk != null)
                    {
                        DecrementChunkRef(chunk);
                    }
                }

                _chunks = null;
            }
        }

        protected override void LeakAtFinalizer()
        {
            // TODO:
        }
    }
}
