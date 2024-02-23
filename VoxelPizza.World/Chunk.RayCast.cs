using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class Chunk
    {
        public struct BlockRayCast : IDisposable
        {
            private ValueArc<Chunk> _chunk;
            private StartEndVoxelRayCallback _rayCallback;

            public readonly ValueArc<Chunk> Chunk => _chunk.Wrap();

            public BlockRayCast(ValueArc<Chunk> chunk, bool local)
            {
                _chunk = chunk.Wrap();

                if (local)
                {
                    _rayCallback = new StartEndVoxelRayCallback(default, Size.ToInt3());
                }
                else
                {
                    Int3 position = _chunk.Get().Position.ToBlock().ToInt3();
                    _rayCallback = new StartEndVoxelRayCallback(position, position + Size.ToInt3());
                }
            }

            public bool MoveNext(ref VoxelRayCast state)
            {
                //BlockStorage storage = Chunk.GetBlockStorage();

                bool move = state.MoveNext(ref _rayCallback);

                return move;
            }

            public void Dispose()
            {
                _chunk.Dispose();
            }
        }
    }
}
