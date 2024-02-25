using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace VoxelPizza.Numerics
{
    public readonly struct BoundingFrustum4
    {
        public const int PlaneCount = 6;

        public readonly Plane4 Left;
        public readonly Plane4 Right;
        public readonly Plane4 Bottom;
        public readonly Plane4 Top;
        public readonly Plane4 Near;
        public readonly Plane4 Far;

        public static BoundingFrustum4 CreateNormalizedFromMatrix(in Matrix4x4 m)
        {
            // Plane computations: http://gamedevs.org/uploads/fast-extraction-viewing-frustum-planes-from-world-view-projection-matrix.pdf
            Plane4 left = Plane4.Normalize(
                new Plane4(
                    m.M14 + m.M11,
                    m.M24 + m.M21,
                    m.M34 + m.M31,
                    m.M44 + m.M41));

            Plane4 right = Plane4.Normalize(
                new Plane4(
                    m.M14 - m.M11,
                    m.M24 - m.M21,
                    m.M34 - m.M31,
                    m.M44 - m.M41));

            Plane4 bottom = Plane4.Normalize(
                new Plane4(
                    m.M14 + m.M12,
                    m.M24 + m.M22,
                    m.M34 + m.M32,
                    m.M44 + m.M42));

            Plane4 top = Plane4.Normalize(
                new Plane4(
                    m.M14 - m.M12,
                    m.M24 - m.M22,
                    m.M34 - m.M32,
                    m.M44 - m.M42));

            Plane4 near = Plane4.Normalize(
                new Plane4(
                    m.M13,
                    m.M23,
                    m.M33,
                    m.M43));

            Plane4 far = Plane4.Normalize(
                new Plane4(
                    m.M14 - m.M13,
                    m.M24 - m.M23,
                    m.M34 - m.M33,
                    m.M44 - m.M43));

            return new BoundingFrustum4(left, right, bottom, top, near, far);
        }

        public BoundingFrustum4(Plane4 left, Plane4 right, Plane4 bottom, Plane4 top, Plane4 near, Plane4 far)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            Near = near;
            Far = far;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ContainmentType Contains(Vector4 point)
        {
            if (Plane4.DotCoordinate(Left, point) < 0 ||
                Plane4.DotCoordinate(Right, point) < 0 ||
                Plane4.DotCoordinate(Bottom, point) < 0 ||
                Plane4.DotCoordinate(Top, point) < 0 ||
                Plane4.DotCoordinate(Near, point) < 0 ||
                Plane4.DotCoordinate(Far, point) < 0)
            {
                return ContainmentType.Disjoint;
            }

            return ContainmentType.Contains;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ContainmentType Contains(Vector3 point)
        {
            return Contains(new Vector4(point, 0));
        }

        public readonly ContainmentType Contains(BoundingSphere4 sphere)
        {
            ContainmentType result = ContainmentType.Contains;

            for (int i = 0; i < PlaneCount; i++)
            {
                Plane4 plane = Unsafe.Add(ref Unsafe.AsRef(in Left), i);

                float distance = Plane4.DotCoordinate(plane, sphere.ToVector4());
                if (distance < -sphere.Radius)
                    return ContainmentType.Disjoint;
                else if (distance < sphere.Radius)
                    result = ContainmentType.Intersects;
            }
            return result;
        }

        public readonly ContainmentType Contains(BoundingBox4 box)
        {
            // Approach: http://zach.in.tu-clausthal.de/teaching/cg_literatur/lighthouse3d_view_frustum_culling/index.html

            ContainmentType result = ContainmentType.Contains;

            Vector128<float> boxMin = box.Min.AsVector128();
            Vector128<float> boxMax = box.Max.AsVector128();

            for (int i = 0; i < PlaneCount; i++)
            {
                Plane4 plane = Unsafe.Add(ref Unsafe.AsRef(in Left), i);

                Vector128<float> normal = plane.Normal.AsVector128();

                Vector128<float> compare = Vector128.GreaterThanOrEqual(normal, Vector128<float>.Zero);

                // If the positive vertex is outside (behind plane), the box is disjoint.
                Vector128<float> positive = Vector128.ConditionalSelect(compare, boxMax, boxMin);
                float positiveDistance = plane.D + Vector128.Dot(normal, positive);
                if (positiveDistance < 0)
                {
                    return ContainmentType.Disjoint;
                }

                // If the negative vertex is outside (behind plane), the box is intersecting.
                // Because the above check failed, the positive vertex is in front of the plane,
                // and the negative vertex is behind. Thus, the box is intersecting this plane.
                Vector128<float> negative = Vector128.ConditionalSelect(compare, boxMin, boxMax);
                float negativeDistance = plane.D + Vector128.Dot(normal, negative);
                if (negativeDistance < 0)
                {
                    result = ContainmentType.Intersects;
                }
            }

            return result;
        }

        public readonly ContainmentType Contains(in BoundingFrustum4 other)
        {
            int pointsContained = 0;
            other.GetCorners(out FrustumCorners4 corners);

            for (int i = 0; i < FrustumCorners4.CornerCount; i++)
            {
                Vector4 point = Unsafe.Add(ref corners.NearTopLeft, i);
                if (Contains(point) != ContainmentType.Disjoint)
                {
                    pointsContained++;
                }
                else if (pointsContained > 0)
                {
                    // Break early since we won't have all points.
                    break;
                }
            }

            return pointsContained switch
            {
                FrustumCorners4.CornerCount => ContainmentType.Contains,
                0 => ContainmentType.Disjoint,
                _ => ContainmentType.Intersects
            };
        }

        public readonly void GetCorners(out FrustumCorners4 corners)
        {
            corners.NearTopLeft = PlaneIntersection(Near, Top, Left);
            corners.NearTopRight = PlaneIntersection(Near, Top, Right);
            corners.NearBottomLeft = PlaneIntersection(Near, Bottom, Left);
            corners.NearBottomRight = PlaneIntersection(Near, Bottom, Right);
            corners.FarTopLeft = PlaneIntersection(Far, Top, Left);
            corners.FarTopRight = PlaneIntersection(Far, Top, Right);
            corners.FarBottomLeft = PlaneIntersection(Far, Bottom, Left);
            corners.FarBottomRight = PlaneIntersection(Far, Bottom, Right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 PlaneIntersection(Plane4 p1, Plane4 p2, Plane4 p3)
        {
            // Formula: http://geomalgorithms.com/a05-_intersect-1.html
            // The formula assumes that there is only a single intersection point.
            // Because of the way the frustum planes are constructed, this should be guaranteed.
            Vector3 intersection =
                (-(p1.D * Cross(p2.Normal, p3.Normal))
                - (p2.D * Cross(p3.Normal, p1.Normal))
                - (p3.D * Cross(p1.Normal, p2.Normal)))
                / Vector3.Dot(p1.Normal, Cross(p2.Normal, p3.Normal));
            return intersection.AsVector128().AsVector4();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 Cross(Vector3 vector1, Vector3 vector2)
        {
            Vector128<float> v1 = vector1.AsVector128();
            Vector128<float> v2 = vector2.AsVector128();

            Vector128<float> left1 = Vector128.Shuffle(v1, Vector128.Create(1, 2, 0, 3));
            Vector128<float> left2 = Vector128.Shuffle(v2, Vector128.Create(2, 0, 1, 3));

            Vector128<float> right1 = Vector128.Shuffle(v1, Vector128.Create(2, 0, 1, 3));
            Vector128<float> right2 = Vector128.Shuffle(v2, Vector128.Create(1, 2, 0, 3));

            return ((left1 * left2) - (right1 * right2)).AsVector3();
        }
    }
}
