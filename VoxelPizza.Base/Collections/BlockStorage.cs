using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Collections
{
    public abstract class BlockStorage : IBlockStorage
    {
        public abstract BlockStorageType StorageType { get; }
        public abstract ushort Width { get; }
        public abstract ushort Height { get; }
        public abstract ushort Depth { get; }
        public abstract bool IsEmpty { get; }

        public abstract void GetBlockRow(nuint index, ref uint destination, nuint length);

        public abstract void GetBlockRow(nuint x, nuint y, nuint z, ref uint destination, nuint length);

        public abstract void SetBlock(nuint index, uint value);

        public abstract void SetBlock(nuint x, nuint y, nuint z, uint value);

        public abstract void SetBlockLayer(nuint y, uint value);

        public abstract bool TryGetInline(out Span<byte> inlineSpan, out BlockStorageType storageType);

        public static void Expand8To32(ref byte src, ref byte dst, nuint len)
        {
            if (Sse2.IsSupported)
            {
                for (nuint i = 0; i < len / (nuint)Vector128<byte>.Count; i++)
                {
                    Vector128<byte> v1_8 = Unsafe.ReadUnaligned<Vector128<byte>>(ref src);

                    Vector128<ushort> v1_16 = Sse2.UnpackLow(v1_8, Vector128<byte>.Zero).AsUInt16();
                    Vector128<ushort> v2_16 = Sse2.UnpackHigh(v1_8, Vector128<byte>.Zero).AsUInt16();

                    Vector128<uint> v1_32 = Sse2.UnpackLow(v1_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v2_32 = Sse2.UnpackHigh(v1_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v3_32 = Sse2.UnpackLow(v2_16, Vector128<ushort>.Zero).AsUInt32();
                    Vector128<uint> v4_32 = Sse2.UnpackHigh(v2_16, Vector128<ushort>.Zero).AsUInt32();

                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 0 * Unsafe.SizeOf<Vector128<uint>>()), v1_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 1 * Unsafe.SizeOf<Vector128<uint>>()), v2_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * Unsafe.SizeOf<Vector128<uint>>()), v3_32);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 3 * Unsafe.SizeOf<Vector128<uint>>()), v4_32);

                    src = ref Unsafe.Add(ref src, 4 * Unsafe.SizeOf<Vector128<byte>>());
                    dst = ref Unsafe.Add(ref dst, 4 * Unsafe.SizeOf<Vector128<uint>>());
                }

                len %= (nuint)Vector128<byte>.Count;
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

        public abstract nuint GetIndex(nuint x, nuint y, nuint z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint GetIndexBase(nuint depth, nuint width, nuint y, nuint z)
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
    }
}
