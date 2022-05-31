using System.Collections;
using System.Collections.Generic;

namespace VoxelPizza.World
{
    public readonly partial struct ChunkBox
    {
        public struct Enumerator : IEnumerator<ChunkPosition>
        {
            private ChunkPosition _position;
            private ChunkPosition _current;

            public readonly ChunkPosition Origin;
            public readonly ChunkPosition Max;

            public readonly ChunkPosition Current => _current;

            readonly object IEnumerator.Current => Current;

            public Enumerator(ChunkPosition origin, ChunkPosition max)
            {
                Origin = origin;
                Max = max;

                _position = origin;
                _current = origin;
            }

            public bool MoveNext()
            {
                if (_position.X < Max.X)
                {
                    _current.X = _position.X;
                    _position.X++;
                    return true;
                }
                return MoveNextZY();
            }

            private bool MoveNextZY()
            {
                if (_position.Z < Max.Z)
                {
                    _position.X = Origin.X;
                    _current.Z = _position.Z;
                    _position.Z++;
                    return MoveNext();
                }

                if (_position.Y < Max.Y)
                {
                    _position.Z = Origin.Z;
                    _current.Y = _position.Y;
                    _position.Y++;
                    return MoveNext();
                }

                return false;
            }

            public void Reset()
            {
                _position = Origin;
                _current = Origin;
            }

            public void Dispose()
            {
            }
        }
    }
}