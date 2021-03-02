using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public struct HalfVector4 : IEquatable<HalfVector4>
    {
        public static HalfVector4 Zero => default;
        public static HalfVector4 One { get; } = new HalfVector4(1, 1, 1, 1);

        public Half X;
        public Half Y;
        public Half Z;
        public Half W;

        public HalfVector4(Half x, Half y, Half z, Half w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public HalfVector4(float x, float y, float z, float w) : this((Half)x, (Half)y, (Half)z, (Half)w)
        {
        }

        public HalfVector4(Vector3 vector, Half w) : this((Half)vector.X, (Half)vector.Y, (Half)vector.Z, w)
        {
        }

        public HalfVector4(Vector3 vector, float w) : this(vector, (Half)w)
        {
        }

        public HalfVector4(Vector4 vector) : this(vector.X, vector.Y, vector.Z, vector.W)
        {
        }

        public readonly Vector3 ToVector3()
        {
            return new Vector3((float)X, (float)Y, (float)Z);
        }

        public readonly Vector4 ToVector4()
        {
            return new Vector4((float)X, (float)Y, (float)Z, (float)W);
        }

        public readonly bool Equals(HalfVector4 other)
        {
            return X == other.X
                && Y == other.Y
                && Z == other.Z
                && W == other.W;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is HalfVector4 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, W);
        }

        public static bool operator ==(HalfVector4 left, HalfVector4 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HalfVector4 left, HalfVector4 right)
        {
            return !(left == right);
        }
    }
}
