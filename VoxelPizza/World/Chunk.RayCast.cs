using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class Chunk
    {
        public BlockRayCast CastBlockRay(bool local)
        {
            return new BlockRayCast(this.TrackRef(), local);
        }

        public struct BlockRayCast : IDisposable
        {
            private RefCounted<Chunk> _chunk;
            private StartEndVoxelRayCallback _rayCallback;

            public Chunk Chunk => _chunk.Value;

            public BlockRayCast(RefCounted<Chunk> chunk, bool local)
            {
                _chunk = chunk.HasValue ? chunk : throw new ArgumentNullException(nameof(chunk));

                if (local)
                {
                    _rayCallback = new StartEndVoxelRayCallback(default, Size.ToInt3());
                }
                else
                {
                    Int3 position = _chunk.Value.Position.ToBlock().ToInt3();
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
                _chunk.Invalidate();
            }
        }
    }
}
