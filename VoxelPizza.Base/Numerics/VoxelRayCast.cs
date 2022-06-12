using System;
using System.Numerics;

namespace VoxelPizza.Numerics
{
    public struct VoxelRayCast
    {
        public readonly Int3 Step;

        public readonly Vector3 DistanceDelta;

        private bool _result;

        public Int3 Current;

        public Int3 Face;

        public Vector3 DistanceMax;

        //public float Radius
        //{
        //    get => _radius;
        //    set
        //    {
        //        _radius = value;
        //
        //        // Rescale from units of 1 cube-edge to units of 'direction' so we can
        //        // compare with 't'.
        //        _tRadius = value / Direction.Length();
        //    }
        //}

        public VoxelRayCast(Vector3 origin, Vector3 direction)
        {
            // Avoids an infinite loop.
            if (direction.X == 0 && direction.Y == 0 && direction.Y == 0)
                direction.X = 1;

            _result = false;

            //_radius = float.PositiveInfinity;
            //_tRadius = float.PositiveInfinity;

            // Cube containing origin point.
            Current.X = (int)MathF.Floor(origin.X);
            Current.Y = (int)MathF.Floor(origin.Y);
            Current.Z = (int)MathF.Floor(origin.Z);

            Face = default;

            DistanceMax.X = intbound(origin.X, direction.X);
            DistanceMax.Y = intbound(origin.Y, direction.Y);
            DistanceMax.Z = intbound(origin.Z, direction.Z);

            // Direction to increment x,y,z when stepping.
            Step.X = signum(direction.X);
            Step.Y = signum(direction.Y);
            Step.Z = signum(direction.Z);

            // See description above. The initial values depend on the fractional
            // part of the origin.
            // The change in t when taking a step (always positive).
            DistanceDelta.X = Step.X / direction.X;
            DistanceDelta.Y = Step.Y / direction.Y;
            DistanceDelta.Z = Step.Z / direction.Z;
        }

        public bool MoveNext<TCallback>(ref TCallback callback)
            where TCallback : IRayCallback<VoxelRayCast>
        {
            // From "A Fast Voxel Traversal Algorithm for Ray Tracing"
            // by John Amanatides and Andrew Woo, 1987
            // <http://www.cse.yorku.ca/~amana/research/grid.pdf>
            // <http://citeseer.ist.psu.edu/viewdoc/summary?doi=10.1.1.42.3443>
            // Extensions to the described algorithm:
            //   • Imposed a distance limit.
            //   • The face passed through to reach the current cube is provided to
            //     the callback.

            // The foundation of this algorithm is a parameterized representation of
            // the provided ray; origin + t * direction,
            // except that t is not actually stored; rather, at any given point in the
            // traversal, we keep track of the *greater* t values which we would have
            // if we took a step sufficient to cross a cube boundary along that axis
            // (i.e. change the integer part of the coordinate) in _tMax.

            while (
                // ray has not gone past bounds of world
                (Step.X > 0 ? Current.X < callback.EndX : Current.X >= callback.StartX) &&
                (Step.Y > 0 ? Current.Y < callback.EndY : Current.Y >= callback.StartY) &&
                (Step.Z > 0 ? Current.Z < callback.EndZ : Current.Z >= callback.StartZ))
            {
                if (!_result)
                {
                    // Invoke the callback, unless we are not *yet* within the bounds of the world.
                    if (Current.X >= callback.StartX && Current.Y >= callback.StartY && Current.Z >= callback.StartZ &&
                        Current.X < callback.EndX && Current.Y < callback.EndY && Current.Z < callback.EndZ)
                    {
                        _result = true;
                        break;
                    }
                }
                _result = false;

                // _tMax.X stores the t-value at which we cross a cube boundary along the
                // X axis, and similarly for Y and Z. Therefore, choosing the least tMax
                // chooses the closest cube boundary. Only the first case of the four
                // has been commented in detail.
                if (DistanceMax.X < DistanceMax.Y)
                {
                    if (DistanceMax.X < DistanceMax.Z)
                    {
                        if (callback.BreakOnX(ref this)) // _tMax.X > _tRadius
                            break;
                        // Update which cube we are now in.
                        Current.X += Step.X;
                        // Adjust _tMax.X to the next X-oriented boundary crossing.
                        DistanceMax.X += DistanceDelta.X;
                        // Record the normal vector of the cube face we entered.
                        Face.X = -Step.X;
                        Face.Y = 0;
                        Face.Z = 0;
                    }
                    else
                    {
                        if (callback.BreakOnZ(ref this)) // _tMax.Z > _tRadius
                            break;
                        Current.Z += Step.Z;
                        DistanceMax.Z += DistanceDelta.Z;
                        Face.X = 0;
                        Face.Y = 0;
                        Face.Z = -Step.Z;
                    }
                }
                else
                {
                    if (DistanceMax.Y < DistanceMax.Z)
                    {
                        if (callback.BreakOnY(ref this)) // _tMax.Y > _tRadius
                            break;
                        Current.Y += Step.Y;
                        DistanceMax.Y += DistanceDelta.Y;
                        Face.X = 0;
                        Face.Y = -Step.Y;
                        Face.Z = 0;
                    }
                    else
                    {
                        // Identical to the second case, repeated for simplicity in
                        // the conditionals.
                        if (callback.BreakOnZ(ref this)) // _tMax.Z > _tRadius
                            break;
                        Current.Z += Step.Z;
                        DistanceMax.Z += DistanceDelta.Z;
                        Face.X = 0;
                        Face.Y = 0;
                        Face.Z = -Step.Z;
                    }
                }
            }

            return _result;
        }

        private static float intbound(float s, float ds)
        {
            // Find the smallest positive t such that s+t*ds is an integer.
            if (ds < 0)
            {
                return intbound(-s, -ds);
            }
            else
            {
                s = mod(s, 1);
                // problem is now s+t*ds = 1
                return (1 - s) / ds;
            }
        }

        private static int signum(float x)
        {
            return x > 0 ? 1 : x < 0 ? -1 : 0;
        }

        private static float mod(float value, float modulus)
        {
            return (value % modulus + modulus) % modulus;
        }
    }
}
