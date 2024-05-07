using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Numerics
{
    public static class IntMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Abs(int x)
        {
            int sign = x >> 31;
            return (x ^ sign) - sign;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideRoundDown(int a, int b)
        {
            (int q, int r) = Math.DivRem(a, b);
            return q + (r >> 31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(uint value)
        {
            return value != 0 && (value & value - 1) == 0;
        }

        /// <summary>Returns the integer (ceiling) log of the specified value, base 2.</summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2Ceiling(uint value)
        {
            int result = BitOperations.Log2(value);
            if (BitOperations.PopCount(value) != 1)
            {
                result++;
            }
            return result;
        }

        /// <summary>Returns the integer (ceiling) log of the specified value, base 2.</summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2Ceiling(ulong value)
        {
            int result = BitOperations.Log2(value);
            if (BitOperations.PopCount(value) != 1)
            {
                result++;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SmallestPowerOfTwo(uint value)
        {
            uint i = value - 1;
            i |= i >> 1;
            i |= i >> 2;
            i |= i >> 4;
            i |= i >> 8;
            i |= i >> 16;
            return i + 1;
        }

        /// <summary>
        /// A very fast generator passing BigCrush, and can be useful when you want 64 bits of state.
        /// </summary>
        /// <remarks>
        /// This is a fixed-increment version of Java 8's SplittableRandom generator.
        /// See <a href="http://dx.doi.org/10.1145/2714064.2660195">article</a> and 
        /// <a href="http://docs.oracle.com/javase/8/docs/api/java/util/SplittableRandom.html">java doc</a>.
        /// </remarks>
        /// <param name="state">The state can be seeded with any (upto) 64 bit integer value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SplitMix64(ulong state)
        {
            state += 0x9e3779b97f4a7c15;              // increment the state variable
            ulong z = state;                          // copy the state to a working variable
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9; // xor the variable with the variable right bit shifted 30 then multiply by a constant
            z = (z ^ (z >> 27)) * 0x94d049bb133111eb; // xor the variable with the variable right bit shifted 27 then multiply by a constant
            return z ^ (z >> 31);                     // return the variable xored with itself right bit shifted 31
        }
    }
}
