using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace VoxelPizza.Collections.Blocks
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract partial class BlockStorage : IReadableBlockStorage, IWritableBlockStorage, IDisposable
    {
        public abstract BlockStorageType StorageType { get; }

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }
        public bool IsEmpty { get; protected set; }
        public bool IsDisposed { get; private set; }

        public BlockStorage(int width, int height, int depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Expand8To32(ref readonly byte src, ref uint dst, nuint len)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                nuint i = 0;
                for (; i + (nuint)Vector128<byte>.Count <= len; i += (nuint)Vector128<byte>.Count)
                {
                    Vector128<byte> v1_8 = Vector128.LoadUnsafe(in src);

                    (Vector128<ushort> v1_16, Vector128<ushort> v2_16) = Vector128.Widen(v1_8);

                    (Vector128<uint> v1_32, Vector128<uint> v2_32) = Vector128.Widen(v1_16);
                    (Vector128<uint> v3_32, Vector128<uint> v4_32) = Vector128.Widen(v2_16);

                    v1_32.StoreUnsafe(ref dst, (nuint)(0 * Vector128<uint>.Count));
                    v2_32.StoreUnsafe(ref dst, (nuint)(1 * Vector128<uint>.Count));
                    v3_32.StoreUnsafe(ref dst, (nuint)(2 * Vector128<uint>.Count));
                    v4_32.StoreUnsafe(ref dst, (nuint)(3 * Vector128<uint>.Count));

                    src = ref Unsafe.Add(ref Unsafe.AsRef(in src), Vector128<byte>.Count);
                    dst = ref Unsafe.Add(ref dst, Vector128<byte>.Count);
                }
                len -= i;
            }

            (nuint loops, nuint rem) = Math.DivRem(len, 2);
            for (nuint i = 0; i < loops; i++)
            {
                ushort s = Unsafe.ReadUnaligned<ushort>(in src);
                ulong d = (s & 0xFFu) | ((s & 0xFF00uL) << 24);
                Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref dst), d);

                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), 2 * sizeof(byte));
                dst = ref Unsafe.Add(ref dst, 2);
            }

            if (rem != 0)
            {
                dst = src;
            }
        }

        public static void Expand8To32(ReadOnlySpan<byte> source, Span<uint> destination)
        {
            if (source.Length > destination.Length)
            {
                ThrowDstTooSmall();
            }

            ref byte byteSrc = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(source));
            ref uint uintDst = ref MemoryMarshal.GetReference(destination);
            Expand8To32(ref byteSrc, ref uintDst, (nuint)source.Length);
        }

        public static void Expand16To32(ref readonly byte src, ref uint dst, nuint len)
        {
            (nuint loops, nuint rem) = Math.DivRem(len, 2);
            for (nuint i = 0; i < loops; i++)
            {
                uint s = Unsafe.ReadUnaligned<uint>(in src);
                ulong d = (s & 0xFFFFu) | ((s & 0xFFFF0000uL) << 16);
                Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref dst), d);

                src = ref Unsafe.Add(ref Unsafe.AsRef(in src), 2 * sizeof(ushort));
                dst = ref Unsafe.Add(ref dst, 2);
            }

            if (rem != 0)
            {
                dst = Unsafe.ReadUnaligned<ushort>(in src);
            }
        }

        public static void Expand16To32(ReadOnlySpan<ushort> source, ReadOnlySpan<uint> destination)
        {
            if (source.Length > destination.Length)
            {
                ThrowDstTooSmall();
            }

            ref byte byteSrc = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(source));
            ref uint uintDst = ref MemoryMarshal.GetReference(destination);
            Expand16To32(ref byteSrc, ref uintDst, (nuint)source.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetIndex(nuint x, nuint y, nuint z)
        {
            return GetIndexBase((uint)Depth, (uint)Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int x, int y, int z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
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
