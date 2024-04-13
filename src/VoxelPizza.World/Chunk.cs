using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VoxelPizza.Collections.Blocks;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public partial class Chunk : IDestroyable
    {
        public static BlockStorage EmptyStorage { get; } = new BlockStorage0(Width, Height, Depth);
        public static BlockStorage DestroyedStorage { get; } = new BlockStorage0(Width, Height, Depth);

        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        public static Size3 Size => new(Width, Height, Depth);

        private ValueArc<ChunkRegion> _region;
        private BlockStorage _storage;

        public event ChunkAction? Updated;
        public event ChunkAction? Destroyed;

        public ChunkPosition Position { get; }

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;

        public ValueArc<ChunkRegion> Region => _region.Wrap();

        public bool IsEmpty => _storage.IsEmpty;

        public Chunk(ValueArc<ChunkRegion> region, ChunkPosition position)
        {
            _region = region.Wrap();
            Position = position;

            _storage = EmptyStorage;
        }

        public void InvokeUpdate()
        {
            Updated?.Invoke(this);
        }

        public BlockStorage GetBlockStorage()
        {
            if (_storage == DestroyedStorage)
            {
                throw new InvalidOperationException();
            }
            if (_storage == EmptyStorage)
            {
                _storage = new BlockStorage8(Width, Height, Depth);
            }
            return _storage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetIndex(nuint x, nuint y, nuint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetIndexBase(uint depth, uint width, uint y, uint z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint GetIndexBase(nuint depth, nuint width, nuint y, nuint z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndexBase(int depth, int width, int y, int z)
        {
            return (y * depth + z) * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkX(int blockX)
        {
            return IntMath.DivideRoundDown(blockX, Width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkY(int blockY)
        {
            return IntMath.DivideRoundDown(blockY, Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BlockToChunkZ(int blockZ)
        {
            return IntMath.DivideRoundDown(blockZ, Depth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlockPosition GetLocalBlockPosition(BlockPosition position)
        {
            return new BlockPosition(
                (int)((uint)position.X % Width),
                (int)((uint)position.Y % Height),
                (int)((uint)position.Z % Depth));
        }

        private string GetDebuggerDisplay()
        {
            return $"{nameof(Chunk)}<{_storage.ToSimpleString()}>({Position.ToNumericString()})";
        }

        private void SwapStorage(BlockStorage newStorage)
        {
            Debug.Assert(_storage != newStorage);

            _storage.Dispose();

            _storage = newStorage;
        }

        public void Destroy()
        {
            SwapStorage(DestroyedStorage);
        }
    }
}
