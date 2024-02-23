using System;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World
{
    public partial class Dimension
    {
        public struct BlockRayCast : IDisposable
        {
            private ValueArc<Dimension> _dimension;
            private BlockRayCastStatus _status;
            private ChunkRegion.BlockRayCast _regionBlockRay;
            private bool _encounteredBlocks;

            public readonly ValueArc<Dimension> Dimension => _dimension.Wrap();

            public readonly ValueArc<ChunkRegion> CurrentRegion => _regionBlockRay.Region.Wrap();

            public readonly ValueArc<Chunk> CurrentChunk => _regionBlockRay.CurrentChunk.Wrap();

            public BlockRayCast(ValueArc<Dimension> dimension)
            {
                _dimension = dimension.Wrap();
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

                    ValueArc<ChunkRegion> region = Dimension.Get().GetRegion(regionPos);
                    if (region.HasTarget)
                    {
                        _regionBlockRay.Dispose();
                        _regionBlockRay = new ChunkRegion.BlockRayCast(region);

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
                _dimension.Dispose();
                _regionBlockRay.Dispose();
            }
        }
    }
}
