using System.Numerics;
using System.Runtime.Intrinsics;

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

        private VoxelRayCast(Vector4 origin, Vector4 direction)
        {
            // Avoids an infinite loop.
            if (Vector128.EqualsAll(direction.AsVector128(), Vector128<float>.Zero))
            {
                direction.X = 1;
            }

            _result = false;

            //_radius = float.PositiveInfinity;
            //_tRadius = float.PositiveInfinity;

            // Cube containing origin point.
            Current = Vector128.ConvertToInt32(Vector128.Floor(origin.AsVector128())).AsInt3();

            Face = default;

            DistanceMax = intbound(origin.AsVector128(), direction.AsVector128()).AsVector3();

            // Direction to increment x,y,z when stepping.
            Step = signum(direction.AsVector128()).AsInt3();

            // See description above. The initial values depend on the fractional
            // part of the origin.
            // The change in t when taking a step (always positive).
            DistanceDelta = (Vector128.ConvertToSingle(Step.AsVector128()) / direction.AsVector128()).AsVector3();
        }

        public VoxelRayCast(Vector3 origin, Vector3 direction) : this(new Vector4(origin, 0), new Vector4(direction, 0))
        {
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

            while (true)
            {
                Int3 start = callback.Start;
                Int3 end = callback.End;

                // ray has not gone past bounds of world
                bool inBounds =
                    (Step.X > 0 ? Current.X < end.X : Current.X >= start.X) &&
                    (Step.Y > 0 ? Current.Y < end.Y : Current.Y >= start.Y) &&
                    (Step.Z > 0 ? Current.Z < end.Z : Current.Z >= start.Z);
                if (!inBounds)
                {
                    break;
                }

                if (!_result)
                {
                    // Invoke the callback, unless we are not *yet* within the bounds of the world.
                    if (Current.X >= start.X && Current.Y >= start.Y && Current.Z >= start.Z &&
                        Current.X < end.X && Current.Y < end.Y && Current.Z < end.Z)
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

        private static Vector128<float> intbound(Vector128<float> s, Vector128<float> ds)
        {
            // Find the smallest positive t such that s+t*ds is an integer.
            Vector128<float> condition = Vector128.LessThan(ds, Vector128<float>.Zero);
            Vector128<float> signBit = Vector128.Create(unchecked((int)0x80000000)).AsSingle() & condition;
            s ^= signBit;
            ds ^= signBit;

            Vector128<float> mod = Modulus(s, Vector128<float>.One);
            // problem is now s+t*ds = 1
            return (Vector128<float>.One - mod) / ds;
        }

        private static Vector128<int> signum(Vector128<float> value)
        {
            Vector128<float> gt = Vector128.GreaterThan(value, Vector128<float>.Zero);
            Vector128<float> lt = Vector128.LessThan(value, Vector128<float>.Zero);
            return Vector128.ConditionalSelect(gt.AsInt32(), Vector128<int>.One, lt.AsInt32() & (-Vector128<int>.One));
        }

        private static Vector128<float> Modulus(Vector128<float> value, Vector128<float> modulus)
        {
            return V128Helper.Remainder(V128Helper.Remainder(value, modulus) + modulus, modulus);
        }
    }
}
