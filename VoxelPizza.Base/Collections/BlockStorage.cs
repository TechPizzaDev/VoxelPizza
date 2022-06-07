using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Collections
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract class BlockStorage : IBlockStorage, IDisposable
    {
        public abstract BlockStorageType StorageType { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort Depth { get; }
        public bool IsEmpty { get; protected set; }
        public bool IsDisposed { get; private set; } 

        public BlockStorage(ushort width, ushort height, ushort depth)
        {
            Width = width;
            Height = height;
            Depth = depth;
        }

        public abstract void GetBlockRow(int index, Span<uint> destination);

        public abstract void GetBlockRow(int x, int y, int z, Span<uint> destination);

        public abstract void SetBlock(int index, uint value);

        public abstract void SetBlock(int x, int y, int z, uint value);

        public abstract void SetBlockLayer(int y, uint value);

        public abstract bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Expand8To32(ref byte src, ref byte dst, nuint len)
        {
            if (Sse2.IsSupported)
            {
                nuint i = 0;
                for (; i + (nuint)Vector128<byte>.Count <= len; i += (nuint)Vector128<byte>.Count)
                {
                    Vector128<byte> v1_8 = Unsafe.ReadUnaligned<Vector128<byte>>(ref src);

                    Vector128<ushort> v1_16 = Sse2.UnpackLow(v1_8, Vector128<byte>.Zero).AsUInt16();
                    Vector128<ushort> v2_16 = Sse2.UnpackHigh(v1_8, Vector128<byte>.Zero).AsUInt16();

                    Vector128<uint> v1_32 = Sse2.UnpackLow(v1_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v2_32 = Sse2.UnpackHigh(v1_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v3_32 = Sse2.UnpackLow(v2_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v4_32 = Sse2.UnpackHigh(v2_16, Vector128<ushort>.Zero).AsUInt32();

                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 0 * (nuint)Unsafe.SizeOf<Vector128<uint>>()), v1_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 1 * (nuint)Unsafe.SizeOf<Vector128<uint>>()), v2_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * (nuint)Unsafe.SizeOf<Vector128<uint>>()), v3_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 3 * (nuint)Unsafe.SizeOf<Vector128<uint>>()), v4_32);

                    src = ref Unsafe.Add(ref src, 1 * (nuint)Unsafe.SizeOf<Vector128<byte>>());
                    dst = ref Unsafe.Add(ref dst, 4 * (nuint)Unsafe.SizeOf<Vector128<uint>>());
                }
                len -= i;
            }

            for (nuint i = 0; i < len / 2; i++)
            {
                ushort s = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, i * 2 * sizeof(byte)));
                ulong d = (s & 0xFFu) | ((s & 0xFF00uL) << 24);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 * sizeof(uint)), d);
            }

            if ((len & 1) != 0)
            {
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref dst, (len - 1) * sizeof(uint)),
                    (uint)Unsafe.Add(ref src, (len - 1) * sizeof(byte)));
            }
        }

        public static void Expand16To32(ref byte src, ref byte dst, nuint len)
        {
            nuint loops = len / 2;
            for (nuint i = 0; i < loops; i++)
            {
                uint s = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, i * 2 * sizeof(ushort)));
                ulong d = (s & 0xFFFFu) | ((s & 0xFFFF0000uL) << 16);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 * sizeof(uint)), d);
            }

            if ((len & 1) != 0)
            {
                nuint i = len - 1;
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref dst, i * sizeof(uint)),
                    (uint)Unsafe.Add(ref src, i * sizeof(ushort)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public nuint GetIndex(nuint x, nuint y, nuint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
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

        public static int GetElementSize(BlockStorageType inlineType)
        {
            return inlineType switch
            {
                BlockStorageType.Unsigned8 => 1,
                BlockStorageType.Unsigned16 => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(inlineType))
            };
        }

        public string ToSimpleString()
        {
            string value = StorageType switch
            {
                BlockStorageType.Undefined => "U",
                BlockStorageType.Null => "N",
                BlockStorageType.Specialized => "S",
                BlockStorageType.Unsigned8 => "U8",
                BlockStorageType.Unsigned16 => "U16",
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
    }
}
