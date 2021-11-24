using System;
using System.Linq;
using System.Numerics;

namespace VoxelPizza
{
    /// <summary>
    /// A random number generator that uses a fast algorithm to generate random values.
    /// The speed comes at the price of true 'randomness' though, there are noticeable
    /// patterns and it compares quite unfavourably to other algorithms in that respect.
    /// It's a good choice in situations where speed is more desirable than a
    /// good random distribution, and a poor choice when random distribution is important.
    /// </summary>
    public class FastRandom
    {
        private int _state;

        public FastRandom() : this(1)
        {
        }

        public FastRandom(int seed)
        {
            if (seed < 1)
                throw new ArgumentOutOfRangeException(nameof(seed), "seed must be greater than zero");

            _state = seed;
        }

        /// <summary>
        ///     Gets the next random integer value.
        /// </summary>
        /// <returns>A random positive integer.</returns>
        public int Next()
        {
            _state = 214013 * _state + 2531011;
            return (_state >> 16) & 0x7FFF;
        }

        /// <summary>
        ///     Gets the next random integer value which is greater than zero and less than or equal to
        ///     the specified maxmimum value.
        /// </summary>
        /// <param name="max">The maximum random integer value to return.</param>
        /// <returns>A random integer value between zero and the specified maximum value.</returns>
        public int Next(int max)
        {
            return (int)(max * NextSingle() + 0.5f);
        }

        /// <summary>
        ///     Gets the next random integer between the specified minimum and maximum values.
        /// </summary>
        /// <param name="min">The inclusive minimum value.</param>
        /// <param name="max">The inclusive maximum value.</param>
        public int Next(int min, int max)
        {
            return (int)((max - min) * NextSingle() + 0.5f) + min;
        }

        /// <summary>
        ///     Gets the next random single value.
        /// </summary>
        /// <returns>A random single value between 0 and 1.</returns>
        public float NextSingle()
        {
            return Next() / (float)short.MaxValue;
        }

        /// <summary>
        ///     Gets the next random single value which is greater than zero and less than or equal to
        ///     the specified maxmimum value.
        /// </summary>
        /// <param name="max">The maximum random single value to return.</param>
        /// <returns>A random single value between zero and the specified maximum value.</returns>
        public float NextSingle(float max)
        {
            return max * NextSingle();
        }

        /// <summary>
        ///     Gets the next random single value between the specified minimum and maximum values.
        /// </summary>
        /// <param name="min">The inclusive minimum value.</param>
        /// <param name="max">The inclusive maximum value.</param>
        /// <returns>A random single value between the specified minimum and maximum values.</returns>
        public float NextSingle(float min, float max)
        {
            return (max - min) * NextSingle() + min;
        }

        /// <summary>
        ///     Gets the next random angle value.
        /// </summary>
        /// <returns>A random angle value.</returns>
        public float NextAngle()
        {
            return NextSingle(-MathF.PI, MathF.PI);
        }

        public Vector2 NextUnitVector2()
        {
            float angle = NextAngle();
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        public Vector3 NextUnitVector3()
        {
            float u = NextSingle();
            float v = NextSingle();

            float t = u * 2 * MathF.PI;
            float z = 2 * v - 1;
            float sf = MathF.Sqrt(1 - z * z);
            float x = sf * MathF.Cos(t);
            float y = sf * MathF.Sin(t);

            return new Vector3(x, y, z);
        }
    }
}
