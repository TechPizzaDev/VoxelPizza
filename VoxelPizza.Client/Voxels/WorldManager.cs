using System;
using System.Threading;
using System.Threading.Tasks;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class WorldManager
    {
        public WorldManager()
        {
        }

        public Dimension CreateDimension()
        {
            Dimension dimension = new();
            dimension.IncrementRef(RefCountType.Container);
            return dimension;
        }

        public Task CreateTestWorld(Dimension dimension, bool async)
        {
            //TerrainGenerator generator = new PlaneTerrainGenerator();
            //TerrainGenerator generator = new SphereTerrainGenerator();
            TerrainGenerator generator = new WavesTerrainGenerator();

            void action()
            {
                try
                {
                    if (async)
                        Thread.Sleep(1000);

                    int width = 64;
                    int depth = width;
                    int height = width;

                    Size3 size = new((uint)width, (uint)height, (uint)depth);

                    ChunkPosition currentPosition = dimension.PlayerChunkPosition;
                    ChunkPosition previousPosition = currentPosition;

                    ChunkPosition centerOffset = new(width / 2, height / 2, depth / 2);

                    void AddChunk(ChunkRegion region, ChunkPosition position)
                    {
                        if (!generator.CanGenerate(position))
                        {
                            return;
                        }

                        using RefCounted<Chunk> countedChunk = region.CreateChunk(position, out _);
                        Chunk chunk = countedChunk.Value;

                        if (generator.Generate(chunk))
                        {
                            chunk.InvokeUpdate();
                        }
                    }

                    ChunkPosition currentOrigin = currentPosition - centerOffset;
                    ChunkPosition currentMax = currentPosition + centerOffset;

                    foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                    {
                        using RefCounted<ChunkRegion> region = dimension.CreateRegion(regionSlice.Region);

                        ChunkBox chunkBox = regionSlice.GetChunkBox();
                        ChunkPosition origin = chunkBox.Origin;
                        ChunkPosition max = chunkBox.Max;

                        ChunkPosition position;
                        for (position.Y = origin.Y; position.Y < max.Y; position.Y++)
                        {
                            for (position.Z = origin.Z; position.Z < max.Z; position.Z++)
                            {
                                for (position.X = origin.X; position.X < max.X; position.X++)
                                {
                                    AddChunk(region.Value, position);
                                }
                            }
                        }
                    }

                    while (true)
                    {
                        currentPosition = dimension.PlayerChunkPosition;

                        if (previousPosition == currentPosition)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        currentOrigin = currentPosition - centerOffset;
                        currentMax = currentPosition + centerOffset;
                        ChunkBox currentBox = new(currentOrigin, currentMax);

                        ChunkPosition previousOrigin = previousPosition - centerOffset;
                        ChunkPosition previousMax = previousPosition + centerOffset;
                        ChunkBox previousBox = new(previousOrigin, previousMax);

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(previousOrigin, previousMax))
                        {
                            using RefCounted<ChunkRegion?> region = dimension.GetRegion(regionSlice.Region);
                            if (!region.HasValue)
                            {
                                continue;
                            }

                            ChunkBox chunkBox = regionSlice.GetChunkBox();
                            ChunkPosition origin = chunkBox.Origin;
                            ChunkPosition max = chunkBox.Max;

                            ChunkPosition position;
                            for (position.Y = origin.Y; position.Y < max.Y; position.Y++)
                            {
                                for (position.Z = origin.Z; position.Z < max.Z; position.Z++)
                                {
                                    for (position.X = origin.X; position.X < max.X; position.X++)
                                    {
                                        if (!currentBox.Contains(position))
                                        {
                                            region.Value.RemoveChunk(position);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                        {
                            using RefCounted<ChunkRegion> region = dimension.CreateRegion(regionSlice.Region);

                            ChunkBox chunkBox = regionSlice.GetChunkBox();
                            ChunkPosition origin = chunkBox.Origin;
                            ChunkPosition max = chunkBox.Max;

                            ChunkPosition position;
                            for (position.Y = origin.Y; position.Y < max.Y; position.Y++)
                            {
                                for (position.Z = origin.Z; position.Z < max.Z; position.Z++)
                                {
                                    for (position.X = origin.X; position.X < max.X; position.X++)
                                    {
                                        if (!previousBox.Contains(position))
                                        {
                                            AddChunk(region.Value, position);
                                        }
                                    }
                                }
                            }
                        }

                        previousPosition = currentPosition;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            if (async)
            {
                Task task = new(action, TaskCreationOptions.LongRunning);
                task.Start();
                return task;
            }
            else
            {
                action();
                return Task.CompletedTask;
            }
        }
    }
}
