using System.Collections.Generic;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public delegate void ChunkGraphSidesChanged(
        RenderRegionGraph graph,
        ChunkPosition localPosition,
        ChunkGraphFaces oldFlags,
        ChunkGraphFaces newFlags);

    public class ChunkGraph
    {
        private Dictionary<RenderRegionPosition, RenderRegionGraph> _roots = new();

        public Size3 RegionSize { get; }

        public event ChunkGraphSidesChanged? SidesFulfilled;
        public event ChunkGraphSidesChanged? SidesDisconnected;

        public ChunkGraph(Size3 regionSize)
        {
            RegionSize = regionSize;
        }

        public ChunkGraphFaces AddChunk(ChunkPosition chunkPosition, ChunkGraphFaces flags)
        {
            flags |= ChunkGraphFaces.Center;
            return ActChunkAndSurround(new AddActor(RegionSize), chunkPosition, RegionSize, flags);
        }

        public ChunkGraphFaces RemoveChunk(ChunkPosition chunkPosition)
        {
            ChunkGraphFaces flags = ChunkGraphFaces.Center | ChunkGraphFaces.Empty | ChunkGraphFaces.Update;
            return ActChunkAndSurround(new RemoveActor(RegionSize), chunkPosition, RegionSize, flags);
        }

        public ChunkGraphFaces UpdateChunk(ChunkPosition chunkPosition, ChunkGraphFaces flags)
        {
            return ActGlobal(new UpdateActor(RegionSize), chunkPosition, flags);
        }

        public ChunkGraphFaces GetChunk(ChunkPosition chunkPosition)
        {
            RenderRegionGraph container = GetContainer(chunkPosition);

            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, RegionSize);
            return container.Get(localPosition, RegionSize);
        }

        private RenderRegionGraph GetContainer(ChunkPosition chunkPosition)
        {
            RenderRegionPosition regionPos = new(chunkPosition, RegionSize);

            if (!_roots.TryGetValue(regionPos, out RenderRegionGraph? container))
            {
                container = new RenderRegionGraph(regionPos, RegionSize);
                container.SidesFulfilled += Container_SidesFulfilled;
                container.SidesDisconnected += Container_SidesDisconnected;

                _roots.Add(regionPos, container);
            }
            return container;
        }

        private void Container_SidesFulfilled(
            RenderRegionGraph graph,
            ChunkPosition localPosition,
            ChunkGraphFaces oldFlags,
            ChunkGraphFaces newFlags)
        {
            SidesFulfilled?.Invoke(graph, localPosition, oldFlags, newFlags);
        }

        private void Container_SidesDisconnected(
            RenderRegionGraph graph,
            ChunkPosition localPosition,
            ChunkGraphFaces oldFlags,
            ChunkGraphFaces newFlags)
        {
            SidesDisconnected?.Invoke(graph, localPosition, oldFlags, newFlags);
        }

        private ChunkGraphFaces ActChunkAndSurround<TActor>(
            in TActor actor, ChunkPosition chunkPosition, Size3 regionSize, ChunkGraphFaces flags)
            where TActor : IActor
        {
            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, regionSize);

            RenderRegionGraph localGraph = GetContainer(chunkPosition);
            ChunkGraphFaces result = actor.Act(localGraph, localPosition, flags);

            // TODO: all surround (3x3x3)

            if (localPosition.X == 0)
            {
                ChunkPosition leftChunk = chunkPosition;
                leftChunk.X -= 1;
                ActGlobal(actor, leftChunk, ChunkGraphFaces.Right);
            }
            else
            {
                ChunkPosition leftChunk = localPosition;
                leftChunk.X -= 1;
                actor.Act(localGraph, leftChunk, ChunkGraphFaces.Right);
            }

            if (localPosition.X == regionSize.W - 1)
            {
                ChunkPosition rightChunk = chunkPosition;
                rightChunk.X += 1;
                ActGlobal(actor, rightChunk, ChunkGraphFaces.Left);
            }
            else
            {
                ChunkPosition rightChunk = localPosition;
                rightChunk.X += 1;
                actor.Act(localGraph, rightChunk, ChunkGraphFaces.Left);
            }

            if (localPosition.Y == 0)
            {
                ChunkPosition bottomChunk = chunkPosition;
                bottomChunk.Y -= 1;
                ActGlobal(actor, bottomChunk, ChunkGraphFaces.Top);
            }
            else
            {
                ChunkPosition bottomChunk = localPosition;
                bottomChunk.Y -= 1;
                actor.Act(localGraph, bottomChunk, ChunkGraphFaces.Top);
            }

            if (localPosition.Y == regionSize.H - 1)
            {
                ChunkPosition topChunk = chunkPosition;
                topChunk.Y += 1;
                ActGlobal(actor, topChunk, ChunkGraphFaces.Bottom);
            }
            else
            {
                ChunkPosition topChunk = localPosition;
                topChunk.Y += 1;
                actor.Act(localGraph, topChunk, ChunkGraphFaces.Bottom);
            }

            if (localPosition.Z == 0)
            {
                ChunkPosition backChunk = chunkPosition;
                backChunk.Z -= 1;
                ActGlobal(actor, backChunk, ChunkGraphFaces.Front);
            }
            else
            {
                ChunkPosition backChunk = localPosition;
                backChunk.Z -= 1;
                actor.Act(localGraph, backChunk, ChunkGraphFaces.Front);
            }

            if (localPosition.Z == regionSize.D - 1)
            {
                ChunkPosition frontChunk = chunkPosition;
                frontChunk.Z += 1;
                ActGlobal(actor, frontChunk, ChunkGraphFaces.Back);
            }
            else
            {
                ChunkPosition frontChunk = localPosition;
                frontChunk.Z += 1;
                actor.Act(localGraph, frontChunk, ChunkGraphFaces.Back);
            }

            return result;
        }

        private ChunkGraphFaces ActGlobal<TActor>(in TActor actor, ChunkPosition globalPosition, ChunkGraphFaces flags)
            where TActor : IActor
        {
            RenderRegionGraph container = GetContainer(globalPosition);
            ChunkPosition localPosition = RenderRegionPosition.GetLocalChunkPosition(globalPosition, RegionSize);
            return actor.Act(container, localPosition, flags);
        }

        public static bool IsLocalOnEdge(ChunkPosition localPosition, Size3 regionSize)
        {
            return
                localPosition.X == 0 ||
                localPosition.X == regionSize.W - 1 ||
                localPosition.Y == 0 ||
                localPosition.Y == regionSize.H - 1 ||
                localPosition.Z == 0 ||
                localPosition.Z == regionSize.D - 1;
        }

        private interface IActor
        {
            ChunkGraphFaces Act(RenderRegionGraph container, ChunkPosition localPosition, ChunkGraphFaces flags);
        }

        private readonly struct AddActor(Size3 regionSize) : IActor
        {
            public ChunkGraphFaces Act(RenderRegionGraph container, ChunkPosition localPosition, ChunkGraphFaces flags)
            {
                return container.Add(localPosition, regionSize, flags);
            }
        }

        private readonly struct RemoveActor(Size3 regionSize) : IActor
        {
            public ChunkGraphFaces Act(RenderRegionGraph container, ChunkPosition localPosition, ChunkGraphFaces flags)
            {
                return container.Remove(localPosition, regionSize, flags);
            }
        }

        private readonly struct UpdateActor(Size3 regionSize) : IActor
        {
            public ChunkGraphFaces Act(RenderRegionGraph container, ChunkPosition localPosition, ChunkGraphFaces flags)
            {
                ChunkGraphFaces newFlags = container.Update(
                    localPosition, regionSize, ChunkGraphFaces.Empty | ChunkGraphFaces.Update, flags);

                if ((newFlags & ChunkGraphFaces.All) != ChunkGraphFaces.All)
                {
                    if ((flags & ChunkGraphFaces.Update) == 0)
                    {
                        return container.Update(localPosition, regionSize, ChunkGraphFaces.Update, ChunkGraphFaces.Update);
                    }
                }
                return newFlags;
            }
        }
    }
}
