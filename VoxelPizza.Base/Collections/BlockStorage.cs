using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Collections
{
    public struct BlockStorage : IBlockStorage
    {
        public const int Width = 16;
        public const int Depth = 16;
        public const int Height = 16;

        private int _type;
        public byte[] _inlineStorage;
        private IBlockStorage _externalStorage;

        public BlockStorage(int type)
        {
            _type = type;

            int size = type == 0 ? 1 : (type == 1 ? 2 : 0);

            _inlineStorage = new byte[Height * Depth * Width * size];
            _externalStorage = null!;
        }

        public void GetBlockRow(nint index, ref uint destination, uint length)
        {
            ref byte inline = ref MemoryMarshal.GetArrayDataReference(_inlineStorage);
            if (_type == 0)
            {
                ref byte inline8 = ref Unsafe.Add(ref inline, index);
                Copy8(ref inline8, ref Unsafe.As<uint, byte>(ref destination), length);
            }
            else if (_type == 1)
            {
                ref byte inline16 = ref Unsafe.Add(ref inline, index * sizeof(ushort));
                Copy16(ref inline16, ref Unsafe.As<uint, byte>(ref destination), length);
            }
            else
            {
                _externalStorage.GetBlockRow(index, ref destination, length);
            }
        }

        public void GetBlockRow(nint x, nint y, nint z, ref uint destination, uint length)
        {
            GetBlockRow(GetIndex(x, y, z), ref destination, length);
        }

        public void SetBlockLayer(nint y, uint value)
        {
            if (_type == 0)
            {
                _inlineStorage.AsSpan((int)GetIndex(0, y, 0), Width * Depth).Fill((byte)value);
            }
            else if (_type == 1)
            {
                Span<byte> span = _inlineStorage.AsSpan(
                    (int)GetIndex(0, y, 0) * sizeof(ushort),
                    Width * Depth * sizeof(ushort));
                MemoryMarshal.Cast<byte, ushort>(span).Fill((ushort)value);
            }
            else
            {
                _externalStorage.SetBlockLayer(y, value);
            }
        }

        public void SetBlock(nint index, uint value)
        {
            ref byte inline = ref MemoryMarshal.GetArrayDataReference(_inlineStorage);
            if (_type == 0)
            {
                Unsafe.Add(ref inline, index) = (byte)value;
            }
            else if (_type == 1)
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref inline, index * sizeof(ushort)), (ushort)value);
            }
            else
            {
                _externalStorage.SetBlock(index, value);
            }
        }

        public void SetBlock(nint x, nint y, nint z, uint value)
        {
            SetBlock(GetIndex(x, y, z), value);
        }

        public static void Copy8(ref byte src, ref byte dst, uint len)
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

            nint loops = (nint)len / 2;
            for (nint i = 0; i < loops; i++)
            {
                ushort s = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref src, i * 2 * sizeof(byte)));
                ulong d = (s & 0xFFu) | ((s & 0xFF00uL) << 24);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 * sizeof(uint)), d);
            }

            if ((len & 1) != 0)
            {
                nint i = (nint)(len - 1);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref dst, i * sizeof(uint)),
                    (uint)Unsafe.Add(ref src, i * sizeof(byte)));
            }
        }

        public static void Copy16(ref byte src, ref byte dst, uint len)
        {
            nint loops = (nint)len / 2;
            for (nint i = 0; i < loops; i++)
            {
                uint s = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref src, i * 2 * sizeof(ushort)));
                ulong d = (s & 0xFFFFu) | ((s & 0xFFFF0000uL) << 16);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, i * 2 * sizeof(uint)), d);
            }

            if ((len & 1) != 0)
            {
                nint i = (nint)(len - 1);
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref dst, i * sizeof(uint)),
                    (uint)Unsafe.Add(ref src, i * sizeof(ushort)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetIndex(nint x, nint y, nint z)
        {
            return GetIndexBase(Depth, Width, y, z) + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint GetIndexBase(nint depth, nint width, nint y, nint z)
        {
            return (y * depth + z) * width;
        }
    }
}
