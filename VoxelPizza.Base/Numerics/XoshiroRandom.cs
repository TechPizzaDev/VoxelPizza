// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Modified from .NET source

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Numerics
{
    /// <summary>
    /// Random number generator based on the algorithm from <a href="http://prng.di.unimi.it/xoshiro256starstar.c">xoshiro256starstar.c</a>.
    /// </summary>
    /// <remarks>
    /// Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org).
    /// To the extent possible under law, the author has dedicated all copyright
    /// and related and neighboring rights to this software to the public domain
    /// worldwide. This software is distributed without any warranty.
    /// 
    /// See <a href="http://creativecommons.org/publicdomain/zero/1.0/">creativecommons publicdomain zero 1.0</a>.
    /// </remarks>
    public struct XoshiroRandom
    {
        // NextUInt64 is based on the algorithm from http://prng.di.unimi.it/xoshiro256starstar.c:
        //
        //     Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
        //
        //     To the extent possible under law, the author has dedicated all copyright
        //     and related and neighboring rights to this software to the public domain
        //     worldwide. This software is distributed without any warranty.
        //
        //     See <http://creativecommons.org/publicdomain/zero/1.0/>.

        private ulong _s0, _s1, _s2, _s3;

        /// <summary>
        /// Creates a seeded xoshiro generator.
        /// </summary>
        /// <param name="seed">The seed state.</param>
        public XoshiroRandom(ulong seed)
        {
            _s3 = seed;
            do
            {
                _s0 = IntMath.SplitMix64(_s3);
                _s1 = IntMath.SplitMix64(_s0);
                _s2 = IntMath.SplitMix64(_s1);
                _s3 = IntMath.SplitMix64(_s2);
            }
            while ((_s0 | _s1 | _s2 | _s3) == 0); // at least one value must be non-zero
        }

        /// <summary>
        /// Creates a randomly seeded xoshiro generator.
        /// </summary>
        public XoshiroRandom() : this((ulong)Random.Shared.NextInt64(long.MinValue, long.MaxValue))
        {
        }

        /// <summary>
        /// Produces an unsigned integer with 32 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt;= <see cref="uint.MaxValue"/>].</returns>
        public uint NextFullUInt32()
        {
            return (uint)(NextFullUInt64() >> 32);
        }

        /// <summary>
        /// Produces an unsigned integer with 32 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <see cref="uint.MaxValue"/>].</returns>
        public uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        /// <summary>
        /// Produces an unsigned integer with 64 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt;= <see cref="ulong.MaxValue"/>].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // small-ish hot path used by a handful of "next" methods
        public ulong NextFullUInt64()
        {
            ulong s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;

            ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
            ulong t = s1 << 17;

            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;

            s2 ^= t;
            s3 = BitOperations.RotateLeft(s3, 45);

            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;

            return result;
        }

        /// <summary>
        /// Produces an unsigned integer with 64 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <see cref="ulong.MaxValue"/>].</returns>
        public ulong NextUInt64()
        {
            while (true)
            {
                ulong result = NextFullUInt64() >> 32;
                if (result != ulong.MaxValue)
                {
                    return result;
                }
            }
        }

        /// <summary>
        /// Produces a signed integer with 32 random bits.
        /// </summary>
        /// <returns>A random value in the range [<see cref="int.MinValue"/> &lt;= x &lt;= <see cref="int.MaxValue"/>]</returns>
        public int NextFullInt32()
        {
            ulong result = NextFullUInt64() >> 32;
            return (int)result;
        }

        /// <summary>
        /// Produces a signed integer with 32 random bits.
        /// </summary>
        /// <returns>A random value in the range [<see cref="int.MinValue"/> &lt;= x &lt; <see cref="int.MaxValue"/>]</returns>
        public int NextInt32()
        {
            while (true)
            {
                ulong result = NextFullUInt64() >> 32;
                if (result != int.MaxValue)
                {
                    return (int)result;
                }
            }
        }

        /// <summary>
        /// Produces a positive signed integer with 31 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt;= <see cref="int.MaxValue"/>].</returns>
        public int NextFullInt31()
        {
            ulong result = NextFullUInt64() >> 33;
            return (int)result;
        }

        /// <summary>
        /// Produces a positive signed integer with 31 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <see cref="int.MaxValue"/>].</returns>
        public int NextInt31()
        {
            while (true)
            {
                ulong result = NextFullUInt64() >> 33;
                if (result != int.MaxValue)
                {
                    return (int)result;
                }
            }
        }

        /// <summary>
        /// Produces a random signed integer within a specified range.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <paramref name="maxValue"/>].</returns>
        public int NextInt32(int maxValue)
        {
            if (maxValue > 1)
            {
                // Narrow down to the smallest range [0, 2^bits] that contains maxValue.
                // Then repeatedly generate a value in that outer range until we get one within the inner range.
                int bits = IntMath.Log2Ceiling((uint)maxValue);
                while (true)
                {
                    ulong result = NextFullUInt64() >> (sizeof(ulong) * 8 - bits);
                    if (result < (uint)maxValue)
                    {
                        return (int)result;
                    }
                }
            }

            Debug.Assert(maxValue == 0 || maxValue == 1);
            return 0;
        }

        /// <summary>
        /// Produces a random signed integer within a specified range.
        /// </summary>
        /// <returns>A random value in the range [<paramref name="minValue"/> &lt;= x &lt; <paramref name="maxValue"/>].</returns>
        public int NextInt32(int minValue, int maxValue)
        {
            ulong exclusiveRange = (ulong)((long)maxValue - minValue);

            if (exclusiveRange > 1)
            {
                // Narrow down to the smallest range [0, 2^bits] that contains maxValue.
                // Then repeatedly generate a value in that outer range until we get one within the inner range.
                int bits = IntMath.Log2Ceiling(exclusiveRange);
                while (true)
                {
                    ulong result = NextFullUInt64() >> (sizeof(ulong) * 8 - bits);
                    if (result < exclusiveRange)
                    {
                        return (int)result + minValue;
                    }
                }
            }

            Debug.Assert(minValue == maxValue || minValue + 1 == maxValue);
            return minValue;
        }

        /// <summary>
        /// Produces a signed integer with 64 random bits.
        /// </summary>
        /// <returns>A random value in the range [<see cref="long.MinValue"/> &lt;= x &lt;= <see cref="long.MaxValue"/>].</returns>
        public long NextFullInt64()
        {
            ulong result = NextFullUInt64();
            return (long)result;
        }

        /// <summary>
        /// Produces a signed integer with 64 random bits.
        /// </summary>
        /// <returns>A random value in the range [<see cref="long.MinValue"/> &lt;= x &lt; <see cref="long.MaxValue"/>].</returns>
        public long NextInt64()
        {
            while (true)
            {
                ulong result = NextFullUInt64();
                if (result != ulong.MaxValue)
                {
                    return (long)result;
                }
            }
        }

        /// <summary>
        /// Produces a positive signed integer with 63 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt;= <see cref="long.MaxValue"/>].</returns>
        public long NextFullInt63()
        {
            ulong result = NextFullUInt64() >> 1;
            return (long)result;
        }

        /// <summary>
        /// Produces a positive signed integer with 63 random bits.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <see cref="long.MaxValue"/>].</returns>
        public long NextInt63()
        {
            while (true)
            {
                ulong result = NextFullUInt64() >> 1;
                if (result != long.MaxValue)
                {
                    return (long)result;
                }
            }
        }

        /// <summary>
        /// Produces a random signed integer within a specified range.
        /// </summary>
        /// <returns>A random value in the range [0 &lt;= x &lt; <paramref name="maxValue"/>].</returns>
        public long NextInt64(long maxValue)
        {
            if (maxValue > 1)
            {
                // Narrow down to the smallest range [0, 2^bits] that contains maxValue.
                // Then repeatedly generate a value in that outer range until we get one within the inner range.
                int bits = IntMath.Log2Ceiling((ulong)maxValue);
                while (true)
                {
                    ulong result = NextFullUInt64() >> (sizeof(ulong) * 8 - bits);
                    if (result < (ulong)maxValue)
                    {
                        return (long)result;
                    }
                }
            }

            Debug.Assert(maxValue == 0 || maxValue == 1);
            return 0;
        }

        /// <summary>
        /// Produces a random signed integer within a specified range.
        /// </summary>
        /// <returns>A random value in the range [<paramref name="minValue"/> &lt;= x &lt; <paramref name="maxValue"/>].</returns>
        public long NextInt64(long minValue, long maxValue)
        {
            ulong exclusiveRange = (ulong)(maxValue - minValue);

            if (exclusiveRange > 1)
            {
                // Narrow down to the smallest range [0, 2^bits] that contains maxValue.
                // Then repeatedly generate a value in that outer range until we get one within the inner range.
                int bits = IntMath.Log2Ceiling(exclusiveRange);
                while (true)
                {
                    ulong result = NextFullUInt64() >> (sizeof(ulong) * 8 - bits);
                    if (result < exclusiveRange)
                    {
                        return (long)result + minValue;
                    }
                }
            }

            Debug.Assert(minValue == maxValue || minValue + 1 == maxValue);
            return minValue;
        }

        /// <summary>
        /// Produces a sequence of random bytes.
        /// </summary>
        /// <param name="buffer">The buffer to fill with random values.</param>
        public void NextBytes(Span<byte> buffer)
        {
            ulong s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;

            while (buffer.Length >= sizeof(ulong))
            {
                Unsafe.WriteUnaligned(
                    ref MemoryMarshal.GetReference(buffer),
                    BitOperations.RotateLeft(s1 * 5, 7) * 9);

                // Update PRNG state.
                ulong t = s1 << 17;
                s2 ^= s0;
                s3 ^= s1;
                s1 ^= s2;
                s0 ^= s3;
                s2 ^= t;
                s3 = BitOperations.RotateLeft(s3, 45);

                buffer = buffer.Slice(sizeof(ulong));
            }

            if (!buffer.IsEmpty)
            {
                ulong next = BitOperations.RotateLeft(s1 * 5, 7) * 9;
                ref byte remainingBytes = ref Unsafe.As<ulong, byte>(ref next);
                Debug.Assert(buffer.Length < sizeof(ulong));
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = Unsafe.Add(ref remainingBytes, i);
                }

                // Update PRNG state.
                ulong t = s1 << 17;
                s2 ^= s0;
                s3 ^= s1;
                s1 ^= s2;
                s0 ^= s3;
                s2 ^= t;
                s3 = BitOperations.RotateLeft(s3, 45);
            }

            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
        }

        /// <summary>
        /// Produces a random double-precision floating-point.
        /// </summary>
        /// <returns>A random value in the range [0.0 &lt;= x &lt;= 1.0].</returns>
        public double NextFloat64()
        {
            // As described in http://prng.di.unimi.it/:
            // "A standard double (64-bit) floating-point number in IEEE floating point format has 52 bits of significand,
            //  plus an implicit bit at the left of the significand. Thus, the representation can actually store numbers with
            //  53 significant binary digits. Because of this fact, in C99 a 64-bit unsigned integer x should be converted to
            //  a 64-bit double using the expression
            //  (x >> 11) * 0x1.0p-53"
            return (NextFullUInt64() >> 11) * (1.0 / (1ul << 53));
        }

        /// <summary>
        /// Produces a random single-precision floating-point.
        /// </summary>
        /// <returns>A random value in the range [0.0 &lt;= x &lt;= 1.0].</returns>
        public float NextFloat32()
        {
            // Same as above, but with 24 bits instead of 53.
            return (NextFullUInt64() >> 40) * (1.0f / (1u << 24));
        }
    }
}
