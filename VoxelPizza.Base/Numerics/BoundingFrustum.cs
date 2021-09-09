using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelPizza.Numerics
{
    public struct BoundingFrustum
    {
        public Plane Left;
        public Plane Right;
        public Plane Bottom;
        public Plane Top;
        public Plane Near;
        public Plane Far;

        public BoundingFrustum(Matrix4x4 m)
        {
            // Plane computations: http://gamedevs.org/uploads/fast-extraction-viewing-frustum-planes-from-world-view-projection-matrix.pdf
            Left = Plane.Normalize(
                new Plane(
                    m.M14 + m.M11,
                    m.M24 + m.M21,
                    m.M34 + m.M31,
                    m.M44 + m.M41));

            Right = Plane.Normalize(
                new Plane(
                    m.M14 - m.M11,
                    m.M24 - m.M21,
                    m.M34 - m.M31,
                    m.M44 - m.M41));

            Bottom = Plane.Normalize(
                new Plane(
                    m.M14 + m.M12,
                    m.M24 + m.M22,
                    m.M34 + m.M32,
                    m.M44 + m.M42));

            Top = Plane.Normalize(
                new Plane(
                    m.M14 - m.M12,
                    m.M24 - m.M22,
                    m.M34 - m.M32,
                    m.M44 - m.M42));

            Near = Plane.Normalize(
                new Plane(
                    m.M13,
                    m.M23,
                    m.M33,
                    m.M43));

            Far = Plane.Normalize(
                new Plane(
                    m.M14 - m.M13,
                    m.M24 - m.M23,
                    m.M34 - m.M33,
                    m.M44 - m.M43));
        }

        public BoundingFrustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            Near = near;
            Far = far;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ContainmentType Contains(Vector3 point)
        {
            if (Plane.DotCoordinate(Left, point) < 0 ||
                Plane.DotCoordinate(Right, point) < 0 ||
                Plane.DotCoordinate(Bottom, point) < 0 ||
                Plane.DotCoordinate(Top, point) < 0 ||
                Plane.DotCoordinate(Near, point) < 0 ||
                Plane.DotCoordinate(Far, point) < 0)
            {
                return ContainmentType.Disjoint;
            }

            return ContainmentType.Contains;
        }

        public readonly ContainmentType Contains(BoundingSphere sphere)
        {
            ContainmentType result = ContainmentType.Contains;

            for (nint i = 0; i < 6 * Unsafe.SizeOf<Plane>(); i += Unsafe.SizeOf<Plane>())
            {
                Plane plane = Unsafe.AddByteOffset(ref Unsafe.AsRef(Left), i);

                float distance = Plane.DotCoordinate(plane, sphere.Center);
                if (distance < -sphere.Radius)
                    return ContainmentType.Disjoint;
                else if (distance < sphere.Radius)
                    result = ContainmentType.Intersects;
            }
            return result;
        }

        public readonly ContainmentType Contains(BoundingBox box)
        {
            // Approach: http://zach.in.tu-clausthal.de/teaching/cg_literatur/lighthouse3d_view_frustum_culling/index.html

            ContainmentType result = ContainmentType.Contains;

            Vector128<float> boxMin = Vector128.AsVector128(box.Min);
            Vector128<float> boxMax = Vector128.AsVector128(box.Max);

            for (nint i = 0; i < 6 * Unsafe.SizeOf<Plane>(); i += Unsafe.SizeOf<Plane>())
            {
                ref Plane plane = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(Left), i);

                if (Sse.IsSupported)
                {
                    Vector128<float> normal = Vector128.AsVector128(plane.Normal);

                    var compare = Sse.CompareGreaterThanOrEqual(normal, Vector128<float>.Zero);
                    var positive = Sse.Or(Sse.AndNot(compare, boxMin), Sse.And(compare, boxMax));
                    var negative = Sse.Or(Sse.AndNot(compare, boxMax), Sse.And(compare, boxMin));

                    // If the positive vertex is outside (behind plane), the box is disjoint.
                    float positiveDistance = plane.D + Vector4.Dot(
                        Vector128.AsVector4(normal), Vector128.AsVector4(positive));

                    if (positiveDistance < 0)
                    {
                        return ContainmentType.Disjoint;
                    }

                    // If the negative vertex is outside (behind plane), the box is intersecting.
                    // Because the above check failed, the positive vertex is in front of the plane,
                    // and the negative vertex is behind. Thus, the box is intersecting this plane.
                    float negativeDistance = plane.D + Vector4.Dot(
                        Vector128.AsVector4(normal), Vector128.AsVector4(negative));

                    if (negativeDistance < 0)
                    {
                        result = ContainmentType.Intersects;
                    }
                }
                else
                {
                    Vector4 normal = new Vector4(plane.Normal, 0);
                    Vector4 positive = new Vector4(box.Min, 0);
                    Vector4 negative = new Vector4(box.Max, 0);

                    if (normal.X >= 0)
                    {
                        positive.X = box.Max.X;
                        negative.X = box.Min.X;
                    }
                    if (normal.Y >= 0)
                    {
                        positive.Y = box.Max.Y;
                        negative.Y = box.Min.Y;
                    }
                    if (normal.Z >= 0)
                    {
                        positive.Z = box.Max.Z;
                        negative.Z = box.Min.Z;
                    }

                    // If the positive vertex is outside (behind plane), the box is disjoint.
                    float positiveDistance = plane.D + Vector4.Dot(normal, positive);
                    if (positiveDistance < 0)
                    {
                        return ContainmentType.Disjoint;
                    }

                    // If the negative vertex is outside (behind plane), the box is intersecting.
                    // Because the above check failed, the positive vertex is in front of the plane,
                    // and the negative vertex is behind. Thus, the box is intersecting this plane.
                    float negativeDistance = plane.D + Vector4.Dot(normal, negative);
                    if (negativeDistance < 0)
                    {
                        result = ContainmentType.Intersects;
                    }
                }
            }

            return result;
        }

        public readonly ContainmentType Contains(in BoundingFrustum other)
        {
            int pointsContained = 0;
            other.GetCorners(out FrustumCorners corners);

            if (Contains(corners.NearTopLeft) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.NearTopRight) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.NearBottomLeft) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.NearBottomRight) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.FarTopLeft) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.FarTopRight) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.FarBottomLeft) != ContainmentType.Disjoint)
                pointsContained++;
            if (Contains(corners.FarBottomRight) != ContainmentType.Disjoint)
                pointsContained++;

            if (pointsContained == 8)
            {
                return ContainmentType.Contains;
            }
            else if (pointsContained == 0)
            {
                return ContainmentType.Disjoint;
            }
            else
            {
                return ContainmentType.Intersects;
            }
        }

        public readonly FrustumCorners GetCorners()
        {
            GetCorners(out FrustumCorners corners);
            return corners;
        }

        public readonly void GetCorners(out FrustumCorners corners)
        {
            PlaneIntersection(Near, Top, Left, out corners.NearTopLeft);
            PlaneIntersection(Near, Top, Right, out corners.NearTopRight);
            PlaneIntersection(Near, Bottom, Left, out corners.NearBottomLeft);
            PlaneIntersection(Near, Bottom, Right, out corners.NearBottomRight);
            PlaneIntersection(Far, Top, Left, out corners.FarTopLeft);
            PlaneIntersection(Far, Top, Right, out corners.FarTopRight);
            PlaneIntersection(Far, Bottom, Left, out corners.FarBottomLeft);
            PlaneIntersection(Far, Bottom, Right, out corners.FarBottomRight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PlaneIntersection(in Plane p1, in Plane p2, in Plane p3, out Vector3 intersection)
        {
            // Formula: http://geomalgorithms.com/a05-_intersect-1.html
            // The formula assumes that there is only a single intersection point.
            // Because of the way the frustum planes are constructed, this should be guaranteed.
            intersection =
                (-(p1.D * Vector3.Cross(p2.Normal, p3.Normal))
                - (p2.D * Vector3.Cross(p3.Normal, p1.Normal))
                - (p3.D * Vector3.Cross(p1.Normal, p2.Normal)))
                / Vector3.Dot(p1.Normal, Vector3.Cross(p2.Normal, p3.Normal));
        }
    }
}
