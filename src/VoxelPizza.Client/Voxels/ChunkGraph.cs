using System.Collections.Generic;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public delegate void ChunkGraphSidesChanged(RenderRegionGraph graph, ChunkPosition localPosition, ChunkGraphFaces newFaces);

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

        public void AddChunk(ChunkPosition chunkPosition, bool isEmpty)
        {
            ChunkGraphFaces flags = ChunkGraphFaces.Center;
            if (isEmpty)
            {
                flags |= ChunkGraphFaces.Empty;
            }
            ActChunkAndSurround(new AddActor(this, chunkPosition), chunkPosition, RegionSize, flags);
        }

        public void RemoveChunk(ChunkPosition chunkPosition)
        {
            ChunkGraphFaces flags = ChunkGraphFaces.Center | ChunkGraphFaces.Empty;
            ActChunkAndSurround(new RemoveActor(this, chunkPosition), chunkPosition, RegionSize, flags);
        }

        public void AddChunkEmptyFlag(ChunkPosition chunkPosition)
        {
            ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, RegionSize);
            new AddActor(this, chunkPosition).ActLocal(localChunkPos, ChunkGraphFaces.Empty);
        }

        public void RemoveChunkEmptyFlag(ChunkPosition chunkPosition)
        {
            ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, RegionSize);
            new RemoveActor(this, chunkPosition).ActLocal(localChunkPos, ChunkGraphFaces.Empty);
        }

        public ChunkGraphFaces GetChunk(ChunkPosition chunkPosition)
        {
            RenderRegionGraph container = GetContainer(chunkPosition);

            ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, RegionSize);
            return container.Get(localChunkPos, RegionSize);
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

        private void Container_SidesFulfilled(RenderRegionGraph graph, ChunkPosition localPosition, ChunkGraphFaces newFaces)
        {
            SidesFulfilled?.Invoke(graph, localPosition, newFaces);
        }

        private void Container_SidesDisconnected(RenderRegionGraph graph, ChunkPosition localPosition, ChunkGraphFaces newFaces)
        {
            SidesDisconnected?.Invoke(graph, localPosition, newFaces);
        }

        private static void ActChunkAndSurround<TActor>(
            TActor actor, ChunkPosition chunkPosition, Size3 regionSize, ChunkGraphFaces faces)
            where TActor : IActor
        {
            ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(chunkPosition, regionSize);

            actor.ActLocal(localChunkPos, faces);

            // TODO: all surround (3x3x3)

            if (localChunkPos.X == 0)
            {
                ChunkPosition leftChunk = chunkPosition;
                leftChunk.X -= 1;
                actor.ActGlobal(leftChunk, ChunkGraphFaces.Right);
            }
            else
            {
                ChunkPosition leftChunk = localChunkPos;
                leftChunk.X -= 1;
                actor.ActLocal(leftChunk, ChunkGraphFaces.Right);
            }

            if (localChunkPos.X == regionSize.W - 1)
            {
                ChunkPosition rightChunk = chunkPosition;
                rightChunk.X += 1;
                actor.ActGlobal(rightChunk, ChunkGraphFaces.Left);
            }
            else
            {
                ChunkPosition rightChunk = localChunkPos;
                rightChunk.X += 1;
                actor.ActLocal(rightChunk, ChunkGraphFaces.Left);
            }

            if (localChunkPos.Y == 0)
            {
                ChunkPosition bottomChunk = chunkPosition;
                bottomChunk.Y -= 1;
                actor.ActGlobal(bottomChunk, ChunkGraphFaces.Top);
            }
            else
            {
                ChunkPosition bottomChunk = localChunkPos;
                bottomChunk.Y -= 1;
                actor.ActLocal(bottomChunk, ChunkGraphFaces.Top);
            }

            if (localChunkPos.Y == regionSize.H - 1)
            {
                ChunkPosition topChunk = chunkPosition;
                topChunk.Y += 1;
                actor.ActGlobal(topChunk, ChunkGraphFaces.Bottom);
            }
            else
            {
                ChunkPosition topChunk = localChunkPos;
                topChunk.Y += 1;
                actor.ActLocal(topChunk, ChunkGraphFaces.Bottom);
            }

            if (localChunkPos.Z == 0)
            {
                ChunkPosition backChunk = chunkPosition;
                backChunk.Z -= 1;
                actor.ActGlobal(backChunk, ChunkGraphFaces.Front);
            }
            else
            {
                ChunkPosition backChunk = localChunkPos;
                backChunk.Z -= 1;
                actor.ActLocal(backChunk, ChunkGraphFaces.Front);
            }

            if (localChunkPos.Z == regionSize.D - 1)
            {
                ChunkPosition frontChunk = chunkPosition;
                frontChunk.Z += 1;
                actor.ActGlobal(frontChunk, ChunkGraphFaces.Back);
            }
            else
            {
                ChunkPosition frontChunk = localChunkPos;
                frontChunk.Z += 1;
                actor.ActLocal(frontChunk, ChunkGraphFaces.Back);
            }
        }

        public static bool IsLocalOnEdge(ChunkPosition localChunkPosition, Size3 regionSize)
        {
            return
                localChunkPosition.X == 0 ||
                localChunkPosition.X == regionSize.W - 1 ||
                localChunkPosition.Y == 0 ||
                localChunkPosition.Y == regionSize.H - 1 ||
                localChunkPosition.Z == 0 ||
                localChunkPosition.Z == regionSize.D - 1;
        }

        private interface IActor
        {
            ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces);
            ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces);
        }

        private struct AddActor : IActor
        {
            private ChunkGraph _graph;
            private RenderRegionGraph _localGraph;

            public AddActor(ChunkGraph graph, ChunkPosition chunkPosition)
            {
                _graph = graph;
                _localGraph = graph.GetContainer(chunkPosition);
            }

            public ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces)
            {
                RenderRegionGraph container = _graph.GetContainer(globalPosition);
                ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(globalPosition, _graph.RegionSize);
                return container.Add(localChunkPos, _graph.RegionSize, faces);
            }

            public ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces)
            {
                return _localGraph.Add(localPosition, _graph.RegionSize, faces);
            }
        }

        private struct RemoveActor : IActor
        {
            private ChunkGraph _graph;
            private RenderRegionGraph _localGraph;

            public RemoveActor(ChunkGraph graph, ChunkPosition chunkPosition)
            {
                _graph = graph;
                _localGraph = graph.GetContainer(chunkPosition);
            }

            public ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces)
            {
                RenderRegionGraph container = _graph.GetContainer(globalPosition);
                ChunkPosition localChunkPos = RenderRegionPosition.GetLocalChunkPosition(globalPosition, _graph.RegionSize);
                return container.Remove(localChunkPos, _graph.RegionSize, faces);
            }

            public ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces)
            {
                return _localGraph.Remove(localPosition, _graph.RegionSize, faces);
            }
        }
    }
}
