using System;
using System.Numerics;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public static class RayExtensions
    {
        public static Vector3 GetPoint(this Ray ray, float distance)
        {
            return ray.Origin + ray.Direction * distance;
        }

        public static bool Intersects(this Ray ray, BoundingBox box, out float distance)
        {
            const float Epsilon = 1e-6f;

            float? tMin = null;
            float? tMax = null;

            distance = 0;

            if (Math.Abs(ray.Direction.X) < Epsilon)
            {
                if (ray.Origin.X < box.Min.X || ray.Origin.X > box.Max.X)
                    return false;
            }
            else
            {
                tMin = (box.Min.X - ray.Origin.X) / ray.Direction.X;
                tMax = (box.Max.X - ray.Origin.X) / ray.Direction.X;

                if (tMin > tMax)
                {
                    var temp = tMin;
                    tMin = tMax;
                    tMax = temp;
                }
            }

            if (Math.Abs(ray.Direction.Y) < Epsilon)
            {
                if (ray.Origin.Y < box.Min.Y || ray.Origin.Y > box.Max.Y)
                    return false;
            }
            else
            {
                var tMinY = (box.Min.Y - ray.Origin.Y) / ray.Direction.Y;
                var tMaxY = (box.Max.Y - ray.Origin.Y) / ray.Direction.Y;

                if (tMinY > tMaxY)
                {
                    var temp = tMinY;
                    tMinY = tMaxY;
                    tMaxY = temp;
                }

                if ((tMin.HasValue && tMin > tMaxY) || (tMax.HasValue && tMinY > tMax))
                    return false;

                if (!tMin.HasValue || tMinY > tMin)
                    tMin = tMinY;
                if (!tMax.HasValue || tMaxY < tMax)
                    tMax = tMaxY;
            }

            if (Math.Abs(ray.Direction.Z) < Epsilon)
            {
                if (ray.Origin.Z < box.Min.Z || ray.Origin.Z > box.Max.Z)
                    return false;
            }
            else
            {
                var tMinZ = (box.Min.Z - ray.Origin.Z) / ray.Direction.Z;
                var tMaxZ = (box.Max.Z - ray.Origin.Z) / ray.Direction.Z;

                if (tMinZ > tMaxZ)
                {
                    var temp = tMinZ;
                    tMinZ = tMaxZ;
                    tMaxZ = temp;
                }

                if ((tMin.HasValue && tMin > tMaxZ) || (tMax.HasValue && tMinZ > tMax))
                    return false;

                if (!tMin.HasValue || tMinZ > tMin)
                    tMin = tMinZ;
                if (!tMax.HasValue || tMaxZ < tMax)
                    tMax = tMaxZ;
            }

            // having a positive tMin and a negative tMax means the ray is inside the box
            // we expect the intesection distance to be 0 in that case
            if (tMin.HasValue && tMin < 0 && tMax > 0)
                return true;

            distance = tMin.GetValueOrDefault();

            // a negative tMin means that the intersection point is behind the ray's origin
            // we discard these as not hitting the AABB
            if (distance < 0)
                return false;

            return true;
        }
    }
}
