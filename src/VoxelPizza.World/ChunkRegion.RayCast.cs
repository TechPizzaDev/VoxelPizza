using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class ChunkRegion
    {
        public struct BlockRayCast : IDisposable
        {
            private ValueArc<ChunkRegion> _region;
            private BlockRayCastStatus _status;
            private Chunk.BlockRayCast _chunkBlockRay;

            public readonly ValueArc<ChunkRegion> Region => _region.Wrap();

            public readonly ValueArc<Chunk> CurrentChunk => _chunkBlockRay.Chunk.Wrap();

            public BlockRayCast(ValueArc<ChunkRegion> region)
            {
                _region = region.Wrap();
                _status = BlockRayCastStatus.Chunk;
                _chunkBlockRay = default;
            }

            public BlockRayCastStatus MoveNext(ref VoxelRayCast state)
            {
                if (_status == BlockRayCastStatus.Block)
                {
                    if (_chunkBlockRay.MoveNext(ref state))
                    {
                        return BlockRayCastStatus.Block;
                    }

                    _status = BlockRayCastStatus.Chunk;
                }
                return TryMoveNext(ref state);
            }

            private BlockRayCastStatus TryMoveNext(ref VoxelRayCast state)
            {
                if (_status == BlockRayCastStatus.Chunk)
                {
                    BlockPosition blockPos = new(
                        state.Current.X,
                        state.Current.Y,
                        state.Current.Z);
                    ChunkPosition chunkPos = blockPos.ToChunk();

                    ChunkRegion region = Region.Get();
                    if (region.GetChunkBox().Contains(chunkPos))
                    {
                        ValueArc<Chunk> chunk = region.GetChunk(chunkPos);
                        if (chunk.HasTarget)
                        {
                            _chunkBlockRay.Dispose();
                            _chunkBlockRay = new Chunk.BlockRayCast(chunk, local: false);

                            _status = BlockRayCastStatus.Block;
                            return BlockRayCastStatus.Chunk;
                        }
                    }
                }

                _status = BlockRayCastStatus.End;
                return BlockRayCastStatus.End;
            }

            public void Dispose()
            {
                _region.Dispose();
                _chunkBlockRay.Dispose();
            }
        }
    }
}
