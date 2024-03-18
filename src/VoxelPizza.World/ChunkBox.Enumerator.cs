namespace VoxelPizza.World
{
    public readonly partial struct ChunkBox
    {
        public struct Enumerator
        {
            private ChunkPosition _position;
            private ChunkPosition _current;

            public readonly ChunkPosition Origin;
            public readonly ChunkPosition Max;

            public readonly ChunkPosition Current => _current;

            public Enumerator(ChunkPosition origin, ChunkPosition max)
            {
                Origin = origin;
                Max = max;

                _position = origin;
                _current = origin;
            }

            public bool MoveNext()
            {
                TryMove:
                if (_position.X < Max.X)
                {
                    _current.X = _position.X;
                    _position.X++;
                    return true;
                }

                if (_position.Z < Max.Z)
                {
                    _position.X = Origin.X;
                    _current.Z = _position.Z;
                    _position.Z++;
                    goto TryMove;
                }

                if (_position.Y < Max.Y)
                {
                    _position.Z = Origin.Z;
                    _current.Y = _position.Y;
                    _position.Y++;
                    goto TryMove;
                }

                return false;
            }

            public void Reset()
            {
                _position = Origin;
                _current = Origin;
            }
        }
    }
}