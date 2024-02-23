using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public partial class ChunkRegion : IDestroyable
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        private Arc<Chunk>?[]? _chunks;
        private int _chunkCount;
        private ReaderWriterLockSlim _chunkLock = new();
        private ChunkAction _cachedChunkUpdated;
        private ChunkAction _cachedChunkDestroyed;

        public Dimension Dimension { get; }
        public ChunkRegionPosition Position { get; }

        public event ChunkAction? ChunkAdded;
        public event ChunkAction? ChunkUpdated;
        public event ChunkAction? ChunkRemoved;

        public event ChunkRegionAction? Empty;
        public event ChunkRegionAction? Destroyed;

        public bool HasChunks => _chunkCount > 0;

        public ChunkRegion(Dimension dimension, ChunkRegionPosition position)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            Position = position;

            _chunks = Array.Empty<Arc<Chunk>>();

            _cachedChunkUpdated = Chunk_ChunkUpdated;
            _cachedChunkDestroyed = Chunk_Destroyed;
        }

        private void Chunk_ChunkUpdated(Chunk chunk)
        {
            ChunkUpdated?.Invoke(chunk);
        }

        public ChunkBox GetChunkBox()
        {
            return new ChunkBox(Position.ToChunk(), Size);
        }

        private Arc<Chunk>?[] GetOrCreateChunkArray()
        {
            if (_chunks == null)
            {
                ThrowObjectDisposedException();
            }

            if (_chunks.Length == 0)
            {
                _chunks = new Arc<Chunk>?[Width * Height * Depth];
            }
            return _chunks;
        }

        private Arc<Chunk>?[] GetChunkArray()
        {
            if (_chunks == null)
            {
                ThrowObjectDisposedException();
            }
            return _chunks;
        }

        public ValueArc<Chunk> GetLocalChunk(int index)
        {
            _chunkLock.EnterReadLock();
            try
            {
                Arc<Chunk>?[] chunks = GetChunkArray();
                if (chunks.Length == 0)
                {
                    return ValueArc<Chunk>.Empty;
                }

                Arc<Chunk>? chunk = chunks[index];
                if (chunk != null)
                {
                    return chunk.Track();
                }
                return ValueArc<Chunk>.Empty;
            }
            finally
            {
                _chunkLock.ExitReadLock();
            }
        }

        public ValueArc<Chunk> GetLocalChunk(ChunkPosition localPosition)
        {
            CheckLocalChunkPosition(localPosition);

            int index = GetChunkIndex(localPosition);
            return GetLocalChunk(index);
        }

        public ValueArc<Chunk> GetChunk(ChunkPosition position)
        {
            CheckChunkPosition(Position, position);

            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);
            return GetLocalChunk(index);
        }

        public ValueArc<Chunk> CreateChunk(ChunkPosition position, out ChunkAddStatus status)
        {
            CheckChunkPosition(Position, position);

            ChunkPosition localPosition = GetLocalChunkPosition(position);
            int index = GetChunkIndex(localPosition);

            ValueArc<Chunk> vChunk = GetLocalChunk(index);
            if (vChunk.HasTarget)
            {
                // GetLocalChunk increments refcount
                status = ChunkAddStatus.Success;
                return vChunk;
            }

            _chunkLock.EnterWriteLock();
            try
            {
                Arc<Chunk>?[] chunks = GetOrCreateChunkArray();

                // Check again after acquiring lock,
                // as a chunk may have been created while we were waiting.
                Arc<Chunk>? chunkArc = chunks[index];
                if (chunkArc == null)
                {
                    Chunk chunk = new(this, position);
                    chunk.Updated += _cachedChunkUpdated;
                    chunk.Destroyed += _cachedChunkDestroyed;

                    chunkArc = new Arc<Chunk>(chunk);
                    chunks[index] = chunkArc;
                    _chunkCount++;

                    ChunkAdded?.Invoke(chunk);
                }

                status = ChunkAddStatus.Success;
                return chunkArc.Track();
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
                Arc<Chunk>?[] chunks = GetChunkArray();
                if (chunks.Length == 0)
                {
                    return ChunkRemoveStatus.MissingChunk;
                }

                Arc<Chunk>? chunk = chunks[index];
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

        private void DecrementChunkRef(Arc<Chunk> chunk)
        {
            // Invoke event before decrementing ref to let
            // others delay the unload.
            ChunkRemoved?.Invoke(chunk.Get());

            chunk.Decrement();
        }

        private void Chunk_Destroyed(Chunk chunk)
        {
            chunk.Updated -= _cachedChunkUpdated;
            chunk.Destroyed -= _cachedChunkDestroyed;

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
                foreach (Arc<Chunk>? chunk in _chunks)
                {
                    if (chunk != null)
                    {
                        DecrementChunkRef(chunk);
                    }
                }

                _chunks = null;
            }
        }

        [DoesNotReturn]
        private void ThrowObjectDisposedException()
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
