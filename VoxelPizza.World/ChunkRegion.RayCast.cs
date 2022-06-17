using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class ChunkRegion
    {
        public BlockRayCast CastBlockRay()
        {
            return new BlockRayCast(this.TrackRef());
        }

        public struct BlockRayCast : IDisposable
        {
            private RefCounted<ChunkRegion> _region;
            private BlockRayCastStatus _status;
            private Chunk.BlockRayCast _chunkBlockRay;

            public ChunkRegion ChunkRegion => _region.Value;
            public Chunk CurrentChunk => _chunkBlockRay.Chunk;

            public BlockRayCast(RefCounted<ChunkRegion> chunkRegion)
            {
                _region = chunkRegion.HasValue ? chunkRegion : throw new ArgumentNullException(nameof(chunkRegion));
                _status = BlockRayCastStatus.Chunk;
                _chunkBlockRay = default;
            }

            public bool MoveNext(ref VoxelRayCast state, out BlockRayCastStatus status)
            {
                if (_status == BlockRayCastStatus.Block)
                {
                    if (_chunkBlockRay.MoveNext(ref state))
                    {
                        status = BlockRayCastStatus.Block;
                        return true;
                    }

                    _status = BlockRayCastStatus.Chunk;
                }
                return TryMoveNext(ref state, out status);
            }

            private bool TryMoveNext(ref VoxelRayCast state, out BlockRayCastStatus status)
            {
                if (_status == BlockRayCastStatus.Chunk)
                {
                    BlockPosition blockPos = new(
                        state.Current.X,
                        state.Current.Y,
                        state.Current.Z);
                    ChunkPosition chunkPos = blockPos.ToChunk();

                    ChunkRegion region = ChunkRegion;
                    if (region.GetChunkBox().Contains(chunkPos))
                    {
                        using RefCounted<Chunk?> countedChunk = region.GetChunk(chunkPos);
                        if (countedChunk.TryGetValue(out Chunk? chunk))
                        {
                            _chunkBlockRay.Dispose();
                            _chunkBlockRay = chunk.CastBlockRay(local: false);

                            _status = BlockRayCastStatus.Block;
                            status = BlockRayCastStatus.Chunk;
                            return true;
                        }
                    }
                }

                _status = BlockRayCastStatus.End;
                status = BlockRayCastStatus.End;
                return false;
            }

            public void Dispose()
            {
                _region.Invalidate();
                _chunkBlockRay.Dispose();
            }
        }
    }
}
