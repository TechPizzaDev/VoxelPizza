using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class Dimension
    {
        public BlockRayCast CastBlockRay()
        {
            return new BlockRayCast(this.TrackRef());
        }

        public struct BlockRayCast : IDisposable
        {
            private RefCounted<Dimension> _region;
            private BlockRayCastStatus _status;
            private ChunkRegion.BlockRayCast _regionBlockRay;
            private bool _encounteredBlocks;

            public Dimension Dimension => _region.Value;
            public ChunkRegion CurrentChunkRegion => _regionBlockRay.ChunkRegion;
            public Chunk CurrentChunk => _regionBlockRay.CurrentChunk;

            public BlockRayCast(RefCounted<Dimension> chunkRegion)
            {
                _region = chunkRegion.HasValue ? chunkRegion : throw new ArgumentNullException(nameof(chunkRegion));
                _status = BlockRayCastStatus.Region;
                _regionBlockRay = default;
                _encounteredBlocks = false;
            }

            public BlockRayCastStatus MoveNext(ref VoxelRayCast state)
            {
                BlockRayCastStatus status = BlockRayCastStatus.Region;
                if (_status == BlockRayCastStatus.Block)
                {
                    status = _regionBlockRay.MoveNext(ref state);
                    if (status != BlockRayCastStatus.End)
                    {
                        _encounteredBlocks = true;
                        return status;
                    }

                    _status = BlockRayCastStatus.Region;
                }
                return TryMoveNext(ref state, status);
            }

            private BlockRayCastStatus TryMoveNext(ref VoxelRayCast state, BlockRayCastStatus status)
            {
                if (_status == BlockRayCastStatus.Region)
                {
                    BlockPosition blockPos = new(
                        state.Current.X,
                        state.Current.Y,
                        state.Current.Z);
                    ChunkPosition chunkPos = blockPos.ToChunk();
                    ChunkRegionPosition regionPos = chunkPos.ToRegion();

                    RefCounted<ChunkRegion?> countedRegion = Dimension.GetRegion(regionPos);
                    if (countedRegion.TryGetValue(out ChunkRegion? region))
                    {
                        _regionBlockRay.Dispose();
                        _regionBlockRay = region.CastBlockRay();

                        if (status != BlockRayCastStatus.End || _encounteredBlocks)
                        {
                            _encounteredBlocks = false;
                            _status = BlockRayCastStatus.Block;
                            return BlockRayCastStatus.Region;
                        }
                    }
                }

                _status = BlockRayCastStatus.End;
                return BlockRayCastStatus.End;
            }

            public void Dispose()
            {
                _region.Invalidate();
                _regionBlockRay.Dispose();
            }
        }
    }
}
