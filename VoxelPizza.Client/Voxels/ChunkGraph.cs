using System.Collections.Generic;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public delegate void ChunkGraphSidesFulfilled(ChunkRegionGraph graph, ChunkPosition localPosition);

    public class ChunkGraph
    {
        private Dictionary<ChunkRegionPosition, ChunkRegionGraph> _roots = new();

        public event ChunkGraphSidesFulfilled? SidesFulfilled;

        public void AddChunk(ChunkPosition chunkPosition, bool isEmpty)
        {
            ChunkGraphFaces flags = ChunkGraphFaces.Center;
            if (isEmpty)
            {
                flags |= ChunkGraphFaces.Empty;
            }
            ActChunkAndSurround(new AddActor(this, chunkPosition), chunkPosition, flags);
        }

        public void RemoveChunk(ChunkPosition chunkPosition)
        {
            ActChunkAndSurround(new RemoveActor(this, chunkPosition), chunkPosition, ChunkGraphFaces.Center | ChunkGraphFaces.Empty);
        }

        public void FlagChunkEmpty(ChunkPosition chunkPosition, bool isEmpty)
        {
            ChunkPosition localChunkPos = ChunkRegion.GetLocalChunkPosition(chunkPosition);
            if (isEmpty)
            {
                new AddActor(this, chunkPosition).ActLocal(localChunkPos, ChunkGraphFaces.Empty);
            }
            else
            {
                new RemoveActor(this, chunkPosition).ActLocal(localChunkPos, ChunkGraphFaces.Empty);
            }
        }

        public ChunkGraphFaces GetChunk(ChunkPosition chunkPosition)
        {
            ChunkRegionGraph container = GetContainer(chunkPosition);

            ChunkPosition localChunkPos = ChunkRegion.GetLocalChunkPosition(chunkPosition);
            return container.Get(localChunkPos);
        }

        private ChunkRegionGraph GetContainer(ChunkPosition chunkPosition)
        {
            ChunkRegionPosition regionPos = chunkPosition.ToRegion();

            if (!_roots.TryGetValue(regionPos, out ChunkRegionGraph? container))
            {
                container = new ChunkRegionGraph(regionPos);
                container.SidesFulfilled += Container_SidesFulfilled;

                _roots.Add(regionPos, container);
            }
            return container;
        }

        private void Container_SidesFulfilled(ChunkRegionGraph arg1, ChunkPosition arg2)
        {
            SidesFulfilled?.Invoke(arg1, arg2);
        }

        private static void ActChunkAndSurround<TActor>(
            TActor actor, ChunkPosition chunkPosition, ChunkGraphFaces faces)
            where TActor : IActor
        {
            ChunkPosition localChunkPos = ChunkRegion.GetLocalChunkPosition(chunkPosition);

            actor.ActLocal(localChunkPos, faces);

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

            if (localChunkPos.X == Chunk.Width - 1)
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

            if (localChunkPos.Y == Chunk.Height - 1)
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

            if (localChunkPos.Z == Chunk.Depth - 1)
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

        public static bool IsLocalOnEdge(ChunkPosition localChunkPosition)
        {
            return
                localChunkPosition.X == 0 ||
                localChunkPosition.X == Chunk.Width - 1 ||
                localChunkPosition.Y == 0 ||
                localChunkPosition.Y == Chunk.Height - 1 ||
                localChunkPosition.Z == 0 ||
                localChunkPosition.Z == Chunk.Depth - 1;
        }

        private interface IActor
        {
            ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces);
            ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces);
        }

        private struct AddActor : IActor
        {
            private ChunkGraph _graph;
            private ChunkRegionGraph _localGraph;

            public AddActor(ChunkGraph graph, ChunkPosition chunkPosition)
            {
                _graph = graph;
                _localGraph = graph.GetContainer(chunkPosition);
            }

            public ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces)
            {
                ChunkRegionGraph container = _graph.GetContainer(globalPosition);
                ChunkPosition localChunkPos = ChunkRegion.GetLocalChunkPosition(globalPosition);
                return container.Add(localChunkPos, faces);
            }

            public ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces)
            {
                return _localGraph.Add(localPosition, faces);
            }
        }

        private struct RemoveActor : IActor
        {
            private ChunkGraph _graph;
            private ChunkRegionGraph _localGraph;

            public RemoveActor(ChunkGraph graph, ChunkPosition chunkPosition)
            {
                _graph = graph;
                _localGraph = graph.GetContainer(chunkPosition);
            }

            public ChunkGraphFaces ActGlobal(ChunkPosition globalPosition, ChunkGraphFaces faces)
            {
                ChunkRegionGraph container = _graph.GetContainer(globalPosition);
                ChunkPosition localChunkPos = ChunkRegion.GetLocalChunkPosition(globalPosition);
                return container.Remove(localChunkPos, faces);
            }

            public ChunkGraphFaces ActLocal(ChunkPosition localPosition, ChunkGraphFaces faces)
            {
                return _localGraph.Remove(localPosition, faces);
            }
        }
    }
}
