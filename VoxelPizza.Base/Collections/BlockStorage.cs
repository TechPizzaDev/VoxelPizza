using System;
using System.Runtime.CompilerServices;
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
            //uint loops = len / (uint)Vector<byte>.Count;
            //for (uint i = 0; i < loops; i++)
            //{
            //    Vector.Widen(
            //        Unsafe.ReadUnaligned<Vector<byte>>(ref src),
            //        out Vector<ushort> vec1U16,
            //        out Vector<ushort> vec2U16);
            //
            //    Vector.Widen(
            //        vec1U16,
            //        out Vector<uint> vec1U32,
            //        out Vector<uint> vec2U32);
            //    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 0 * Unsafe.SizeOf<Vector<uint>>()), vec1U32);
            //    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 1 * Unsafe.SizeOf<Vector<uint>>()), vec2U32);
            //
            //    Vector.Widen(
            //        vec2U16,
            //        out Vector<uint> vec3U32,
            //        out Vector<uint> vec4U32);
            //    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * Unsafe.SizeOf<Vector<uint>>()), vec3U32);
            //    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 3 * Unsafe.SizeOf<Vector<uint>>()), vec4U32);
            //
            //    src = ref Unsafe.Add(ref src, 4 * Unsafe.SizeOf<Vector<byte>>());
            //    dst = ref Unsafe.Add(ref src, 4 * Unsafe.SizeOf<Vector<uint>>());
            //}

            //nint remainder = (nint)(len % Vector<byte>.Count);

            if (Sse2.IsSupported)
            {

            }

            {
                nuint loops = len / 2;
                for (nuint i = 0; i < loops; i++)
                {
                    ushort s = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, i * 2 * sizeof(byte)));
                    ulong d = (s & 0xFFu) | ((s & 0xFF00uL) << 24);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 * sizeof(uint)), d);
                }
            }

            if ((len & 1) != 0)
            {
                nuint i = len - 1;
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref dst, i * sizeof(uint)),
                    (uint)Unsafe.Add(ref src, i * sizeof(byte)));
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
