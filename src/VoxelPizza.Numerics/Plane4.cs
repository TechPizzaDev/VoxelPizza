// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Numerics
{
    public readonly struct Plane4
    {
        private const float NormalizeEpsilon = 1.192092896e-07f; // smallest such that 1.0+NormalizeEpsilon != 1.0

        /// <summary>The normal vector of the plane with the W component being the distance of the plane along its normal from the origin.</summary>
        private readonly Vector4 _value;

        public readonly Vector3 Normal => _value.ToVector3();

        public readonly float D => _value.W;

        /// <summary>Creates a <see cref="Plane4" /> object from the X, Y, and Z components of its normal, and its distance from the origin on that normal.</summary>
        /// <param name="x">The X component of the normal.</param>
        /// <param name="y">The Y component of the normal.</param>
        /// <param name="z">The Z component of the normal.</param>
        /// <param name="d">The distance of the plane along its normal from the origin.</param>
        public Plane4(float x, float y, float z, float d)
        {
            _value = new Vector4(x, y, z, d);
        }

        /// <summary>Creates a <see cref="Plane4" /> object from a specified normal and the distance along the normal from the origin.</summary>
        /// <param name="normal">The plane's normal vector.</param>
        /// <param name="d">The plane's distance from the origin along its normal vector.</param>
        public Plane4(Vector4 normal, float d)
        {
            _value = normal with { W = d };
        }

        /// <summary>Creates a <see cref="Plane4" /> object from a specified normal and the distance along the normal from the origin.</summary>
        /// <param name="normal">The plane's normal vector.</param>
        /// <param name="d">The plane's distance from the origin along its normal vector.</param>
        public Plane4(Vector3 normal, float d)
        {
            _value = new Vector4(normal, d);
        }

        /// <summary>Creates a <see cref="Plane4" /> object from a specified four-dimensional vector.</summary>
        /// <param name="value">A vector whose first three elements describe the normal vector, and whose <see cref="Vector4.W" /> defines the distance along that normal from the origin.</param>
        public Plane4(Vector4 value)
        {
            _value = value;
        }

        public Vector4 ToVector4()
        {
            return _value;
        }

        /// <summary>Creates a <see cref="Plane4" /> object that contains three specified points.</summary>
        /// <param name="point1">The first point defining the plane.</param>
        /// <param name="point2">The second point defining the plane.</param>
        /// <param name="point3">The third point defining the plane.</param>
        /// <returns>The plane containing the three points.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane4 CreateFromPoints(Vector3 point1, Vector3 point2, Vector3 point3)
        {
            Vector3 a = point2 - point1;
            Vector3 b = point3 - point1;

            // N = Cross(a, b)
            Vector3 n = Vector3.Cross(a, b);
            Vector3 normal = Vector3.Normalize(n);

            // D = - Dot(N, point1)
            float d = -Vector3.Dot(normal, point1);

            return new Plane4(normal, d);
        }

        /// <summary>Calculates the dot product of a plane and a 4-dimensional vector.</summary>
        /// <param name="plane">The plane.</param>
        /// <param name="value">The four-dimensional vector.</param>
        /// <returns>The dot product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Plane4 plane, Vector4 value)
        {
            return Vector4.Dot(plane._value, value);
        }

        /// <summary>
        /// Returns the dot product of a specified three-dimensional vector and 
        /// the <see cref="Normal" /> vector of this plane plus the distance (<see cref="D" />) value.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="value">The 3-dimensional vector.</param>
        /// <returns>The dot product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotCoordinate(Plane4 plane, Vector4 value)
        {
            return Vector3.Dot(plane.Normal, value.ToVector3()) + plane.D;
        }

        /// <summary>Returns the dot product of a specified three-dimensional vector and the <see cref="Normal" /> vector of this plane.</summary>
        /// <param name="plane">The plane.</param>
        /// <param name="value">The three-dimensional vector.</param>
        /// <returns>The dot product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotNormal(Plane4 plane, Vector4 value)
        {
            return Vector3.Dot(plane.Normal, value.ToVector3());
        }

        /// <summary>Creates a new <see cref="Plane4" /> object whose normal vector is the source plane's normal vector normalized.</summary>
        /// <param name="value">The source plane.</param>
        /// <returns>The normalized plane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane4 Normalize(Plane4 value)
        {
            float normalLengthSquared = value._value.ToVector3().LengthSquared();
            if (MathF.Abs(normalLengthSquared - 1.0f) < NormalizeEpsilon)
            {
                // It already normalized, so we don't need to farther process.
                return value;
            }
            float normalLength = MathF.Sqrt(normalLengthSquared);
            return new Plane4(value._value / normalLength);
        }

        /// <summary>Transforms a normalized plane by a 4x4 matrix.</summary>
        /// <param name="plane">The normalized plane to transform.</param>
        /// <param name="matrix">The transformation matrix to apply to <paramref name="plane" />.</param>
        /// <returns>The transformed plane.</returns>
        /// <remarks><paramref name="plane" /> must already be normalized so that its <see cref="Normal" /> vector is of unit length before this method is called.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane4 Transform(Plane4 plane, Matrix4x4 matrix)
        {
            if (Matrix4x4.Invert(matrix, out Matrix4x4 m))
            {
                Matrix4x4 trn = Matrix4x4.Transpose(m);
                Vector4 result = Vector4.Transform(plane._value, trn);
                return new(result);
            }
            return new Plane4(new Vector4(float.NaN));
        }

        /// <summary>Transforms a normalized plane by a Quaternion rotation.</summary>
        /// <param name="plane">The normalized plane to transform.</param>
        /// <param name="rotation">The Quaternion rotation to apply to the plane.</param>
        /// <returns>A new plane that results from applying the Quaternion rotation.</returns>
        /// <remarks><paramref name="plane" /> must already be normalized so that its <see cref="Normal" /> vector is of unit length before this method is called.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane4 Transform(Plane4 plane, Quaternion rotation)
        {
            // Compute rotation matrix.
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float wx2 = rotation.W * x2;
            float wy2 = rotation.W * y2;
            float wz2 = rotation.W * z2;
            float xx2 = rotation.X * x2;
            float xy2 = rotation.X * y2;
            float xz2 = rotation.X * z2;
            float yy2 = rotation.Y * y2;
            float yz2 = rotation.Y * z2;
            float zz2 = rotation.Z * z2;

            float m11 = 1.0f - yy2 - zz2;
            float m21 = xy2 - wz2;
            float m31 = xz2 + wy2;

            float m12 = xy2 + wz2;
            float m22 = 1.0f - xx2 - zz2;
            float m32 = yz2 - wx2;

            float m13 = xz2 - wy2;
            float m23 = yz2 + wx2;
            float m33 = 1.0f - xx2 - yy2;

            float x = plane._value.X, y = plane._value.Y, z = plane._value.Z;

            return new Plane4(
                x * m11 + y * m21 + z * m31,
                x * m12 + y * m22 + z * m32,
                x * m13 + y * m23 + z * m33,
                plane._value.W);
        }

        /// <summary>Returns a value that indicates whether two planes are equal.</summary>
        /// <param name="value1">The first plane to compare.</param>
        /// <param name="value2">The second plane to compare.</param>
        /// <returns><see langword="true" /> if <paramref name="value1" /> and <paramref name="value2" /> are equal; otherwise, <see langword="false" />.</returns>
        /// <remarks>The <see cref="op_Equality" /> method defines the operation of the equality operator for <see cref="Plane4" /> objects.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Plane4 value1, Plane4 value2)
        {
            return value1._value == value2._value;
        }

        /// <summary>Returns a value that indicates whether two planes are not equal.</summary>
        /// <param name="value1">The first plane to compare.</param>
        /// <param name="value2">The second plane to compare.</param>
        /// <returns><see langword="true" /> if <paramref name="value1" /> and <paramref name="value2" /> are not equal; otherwise, <see langword="false" />.</returns>
        /// <remarks>The <see cref="op_Inequality" /> method defines the operation of the inequality operator for <see cref="Plane4" /> objects.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Plane4 value1, Plane4 value2)
        {
            return value1._value != value2._value;
        }

        /// <summary>Returns a value that indicates whether this instance and a specified object are equal.</summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns><see langword="true" /> if the current instance and <paramref name="obj" /> are equal; otherwise, <see langword="false" />. If <paramref name="obj" /> is <see langword="null" />, the method returns <see langword="false" />.</returns>
        /// <remarks>This instance and <paramref name="obj" /> may only be equal if <paramref name="obj" /> is of type <see cref="Plane4" />.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals([NotNullWhen(true)] object? obj)
        {
            return (obj is Plane4 other) && Equals(other);
        }

        /// <summary>Returns a value that indicates whether this instance and another plane object are equal.</summary>
        /// <param name="other">The other plane.</param>
        /// <returns><see langword="true" /> if the two planes are equal; otherwise, <see langword="false" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Plane4 other)
        {
            return this == other;
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>The hash code.</returns>
        public override readonly int GetHashCode()
        {
            return _value.GetHashCode();
        }

        /// <summary>Returns the string representation of this plane object.</summary>
        /// <returns>A string that represents this <see cref="Plane4" /> object.</returns>
        /// <remarks>The string representation of a <see cref="Plane4" /> object use the formatting conventions of the current culture to format the numeric values in the returned string. For example, a <see cref="Plane4" /> object whose string representation is formatted by using the conventions of the en-US culture might appear as <c>{Normal:&lt;1.1, 2.2, 3.3&gt; D:4.4}</c>.</remarks>
        public override readonly string ToString()
        {
            return $"{{Normal:{_value.ToVector3()} D:{_value.W}}}";
        }

        public static explicit operator Plane(in Plane4 plane)
        {
            return new(plane._value.ToVector3(), plane._value.W);
        }
    }
}
