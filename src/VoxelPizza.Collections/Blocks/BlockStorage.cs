using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VoxelPizza.Numerics;

namespace VoxelPizza.Collections.Blocks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract partial class BlockStorage : IWritableBlockStorage, IReadableBlockStorage, IDisposable
    {
        public abstract BlockStorageType StorageType { get; }

        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract int Depth { get; }

        public Size3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new((uint)Width, (uint)Height, (uint)Depth);
        }

        public bool IsEmpty { get; protected set; }
        public bool IsDisposed { get; private set; }

        public abstract bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType);

        public virtual uint GetBlock(int x, int y, int z)
        {
            uint result = 0;
            GetBlocks(new Int3(x, y, z), new Size3(1), new Int3(0), new Size3(1), new Span<uint>(ref result));
            return result;
        }

        public abstract void GetBlocks(Int3 offset, Size3 size, Int3 dstOffset, Size3 dstSize, Span<uint> dstSpan);

        public virtual void SetBlock(int x, int y, int z, uint value)
        {
            FillBlock(new Int3(x, y, z), new Size3(1), value);
        }

        public abstract void SetBlocks(Int3 offset, Size3 size, Int3 srcOffset, Size3 srcSize, ReadOnlySpan<uint> srcSpan);

        public abstract void FillBlock(Int3 offset, Size3 size, uint value);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(Int3 offset, Size3 size, T value, Size3 dstSize, Span<T> dstSpan)
        {
            int dstWidth = (int)dstSize.W;
            int dstDepth = (int)dstSize.D;

            int width = (int)size.W;
            int height = (int)size.H;
            int depth = (int)size.D;

            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int dstIdx = GetIndexBase(dstWidth, dstDepth, offset.Y + y, offset.Z + z);
                    Span<T> dst = dstSpan.Slice(dstIdx + offset.X, width);

                    dst.Fill(value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<TFrom, TTo>(
            Int3 srcOffset, Size3 srcSize, ReadOnlySpan<TFrom> srcSpan,
            Int3 dstOffset, Size3 dstSize, Span<TTo> dstSpan,
            Size3 copySize)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            int srcWidth = (int)srcSize.W;
            int srcDepth = (int)srcSize.D;

            int dstWidth = (int)dstSize.W;
            int dstDepth = (int)dstSize.D;

            int width = (int)copySize.W;
            int height = (int)copySize.H;
            int depth = (int)copySize.D;

            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    int srcIdx = GetIndexBase(srcWidth, srcDepth, srcOffset.Y + y, srcOffset.Z + z);
                    ReadOnlySpan<TFrom> src = srcSpan.Slice(srcIdx + srcOffset.X, width);

                    int dstIdx = GetIndexBase(dstWidth, dstDepth, dstOffset.Y + y, dstOffset.Z + z);
                    Span<TTo> dst = dstSpan.Slice(dstIdx + dstOffset.X, width);

                    Convert(src, dst);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Convert<TFrom, TTo>(ReadOnlySpan<TFrom> src, Span<TTo> dst)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            switch (sizeof(TFrom))
            {
                case 1:
                    switch (sizeof(TTo))
                    {
                        case 1:
                            goto Copy;

                        case 4:
                            Expand8To32(MemoryMarshal.Cast<TFrom, byte>(src), MemoryMarshal.Cast<TTo, uint>(dst));
                            return;

                        default:
                            goto Unsupported;
                    }

                case 2:
                    switch (sizeof(TTo))
                    {
                        case 2:
                            goto Copy;

                        case 4:
                            Expand16To32(MemoryMarshal.Cast<TFrom, ushort>(src), MemoryMarshal.Cast<TTo, uint>(dst));
                            return;

                        default:
                            goto Unsupported;
                    }

                case 3:
                    switch (sizeof(TTo))
                    {
                        case 3:
                            goto Copy;

                        case 4:
                            Expand24To32(MemoryMarshal.Cast<TFrom, UInt24>(src), MemoryMarshal.Cast<TTo, uint>(dst));
                            return;

                        default:
                            goto Unsupported;
                    }

                case 4:
                    switch (sizeof(TTo))
                    {
                        case 1:
                            Narrow(MemoryMarshal.Cast<TFrom, uint>(src), MemoryMarshal.Cast<TTo, byte>(dst));
                            return;

                        case 2:
                            Narrow(MemoryMarshal.Cast<TFrom, uint>(src), MemoryMarshal.Cast<TTo, ushort>(dst));
                            return;

                        case 4:
                            goto Copy;

                        default:
                            goto Unsupported;
                    }

                default:
                    goto Unsupported;
            }

            Copy:
            MemoryMarshal.AsBytes(src).CopyTo(MemoryMarshal.AsBytes(dst));
            return;

            Unsupported:
            ThrowUnsupported();

            [DoesNotReturn]
            static void ThrowUnsupported()
            {
                throw new NotSupportedException($"Cannot convert \"{typeof(TFrom)}\" to \"{typeof(TTo)}\".");
            }
        }

        [DoesNotReturn]
        private static void ThrowDstTooSmall()
        {
            throw new ArgumentException(null, "destination");
        }
    }
}
