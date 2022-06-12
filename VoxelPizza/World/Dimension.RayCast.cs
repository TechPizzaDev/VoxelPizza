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

            public Dimension Dimension => _region.Value;
            public ChunkRegion CurrentChunkRegion => _regionBlockRay.ChunkRegion;
            public Chunk CurrentChunk => _regionBlockRay.CurrentChunk;

            public BlockRayCast(RefCounted<Dimension> chunkRegion)
            {
                _region = chunkRegion.HasValue ? chunkRegion : throw new ArgumentNullException(nameof(chunkRegion));
                _status = BlockRayCastStatus.Region;
                _regionBlockRay = default;
            }

            public bool MoveNext(ref VoxelRayCast state, out BlockRayCastStatus status)
            {
                if (_status == BlockRayCastStatus.Block)
                {
                    if (_regionBlockRay.MoveNext(ref state, out status) &&
                        status != BlockRayCastStatus.End)
                    {
                        return true;
                    }

                    _status = BlockRayCastStatus.Region;
                }
                return TryMoveNext(ref state, out status);
            }

            private bool TryMoveNext(ref VoxelRayCast state, out BlockRayCastStatus status)
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

                        _status = BlockRayCastStatus.Block;
                        status = BlockRayCastStatus.Region;
                        return true;
                    }
                }

                _status = BlockRayCastStatus.End;
                status = BlockRayCastStatus.End;
                return false;
            }

            public void Dispose()
            {
            }
        }
    }
}
