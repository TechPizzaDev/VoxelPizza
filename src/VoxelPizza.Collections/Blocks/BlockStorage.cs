using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Collections.Blocks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract partial class BlockStorage : IWritableBlockStorage, IReadableBlockStorage, IDisposable
    {
        public abstract BlockStorageType StorageType { get; }

        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract int Depth { get; }

        public bool IsEmpty { get; protected set; }
        public bool IsDisposed { get; private set; }

        public abstract bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType);

        public abstract uint GetBlock(int x, int y, int z);

        public virtual void GetBlockRow(int x, int y, int z, Span<uint> destination)
        {
            int length = Math.Min(destination.Length, Width - x);
            Span<uint> dst = destination.Slice(0, length);

            for (int i = 0; i < length; i++)
            {
                uint value = GetBlock(x + i, y, z);
                dst[i] = value;
            }
        }

        public virtual void GetBlockLayer(int y, Span<uint> destination)
        {
            for (int z = 0; z < Depth; z++)
            {
                int index = GetIndexBase(Depth, Width, 0, z);
                Span<uint> dst = destination.Slice(index, Width);

                GetBlockRow(0, y, z, dst);
            }
        }

        public abstract void SetBlock(int x, int y, int z, uint value);

        public virtual void SetBlockRow(int x, int y, int z, ReadOnlySpan<uint> source)
        {
            int length = Math.Min(source.Length, Width - x);
            ReadOnlySpan<uint> src = source.Slice(0, length);

            for (int i = 0; i < length; i++)
            {
                uint value = src[i];
                SetBlock(x + i, y, z, value);
            }
        }

        public virtual void SetBlockLayer(int y, ReadOnlySpan<uint> source)
        {
            for (int z = 0; z < Depth; z++)
            {
                int index = GetIndexBase(Depth, Width, 0, z);
                ReadOnlySpan<uint> src = source.Slice(index, Width);

                SetBlockRow(0, y, z, src);
            }
        }

        public virtual void FillBlock(ReadOnlySpan<uint> source)
        {
            for (int y = 0; y < Height; y++)
            {
                int index = GetIndexBase(Depth, Width, y, 0);
                ReadOnlySpan<uint> src = source.Slice(index, Width * Depth);

                SetBlockLayer(y, src);
            }
        }

        public virtual void SetBlockRow(int x, int y, int z, uint value)
        {
            int length = Width - x;

            for (int i = 0; i < length; i++)
            {
                SetBlock(x + i, y, z, value);
            }
        }

        public virtual void SetBlockLayer(int y, uint value)
        {
            for (int z = 0; z < Depth; z++)
            {
                SetBlockRow(0, y, z, value);
            }
        }

        public virtual void FillBlock(uint value)
        {
            for (int y = 0; y < Height; y++)
            {
                SetBlockLayer(y, value);
            }
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

        public string ToSimpleString()
        {
            string value = StorageType switch
            {
                BlockStorageType.Undefined => "U",
                BlockStorageType.Unsigned0 => "U0",
                BlockStorageType.Specialized => "S",
                BlockStorageType.Unsigned8 => "U8",
                BlockStorageType.Unsigned16 => "U16",
                BlockStorageType.Unsigned24 => "U24",
                BlockStorageType.Unsigned32 => "U32",
                _ => StorageType.ToString(),
            };
            if (IsEmpty)
            {
                value += '?';
            }
            return value;
        }

        public override string ToString()
        {
            return $"{StorageType}{(IsEmpty ? "?" : "")} {Width}x{Height}x{Depth}";
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [DoesNotReturn]
        private static void ThrowDstTooSmall()
        {
            throw new ArgumentException(null, "destination");
        }
    }
}
