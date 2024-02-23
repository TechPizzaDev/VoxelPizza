using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            return dimension;
        }

        public Task CreateTestWorld(ValueArc<Dimension> dimension, bool async)
        {
            //TerrainGenerator generator = new PlaneTerrainGenerator();
            //TerrainGenerator generator = new SphereTerrainGenerator();
            TerrainGenerator generator = new WavesTerrainGenerator();

            Dimension dim = dimension.Get();

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

                    ChunkPosition currentPosition = dim.PlayerChunkPosition;
                    ChunkPosition previousPosition = currentPosition;

                    ChunkPosition centerOffsetMin = new(width / 2, height / 2, depth / 2);
                    ChunkPosition centerOffsetMax = new((width + 1) / 2, (height + 1) / 2, (depth + 1) / 2);

                    void AddChunk(ChunkRegion region, ChunkPosition position)
                    {
                        if (!generator.CanGenerate(position))
                        {
                            return;
                        }

                        ValueArc<Chunk> chunkArc = region.CreateChunk(position, out _);

                        if (generator.Generate(chunk))
                        {
                            chunk.InvokeUpdate();
                        }
                    }

                    ChunkPosition currentOrigin = currentPosition - centerOffsetMin;
                    ChunkPosition currentMax = currentPosition + centerOffsetMax;

                    foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                    {
                        using ValueArc<ChunkRegion> regionArc = dim.CreateRegion(regionSlice.Region);
                        if (!regionArc.TryGet(out ChunkRegion? region))
                        {
                            continue;
                        }

                        ChunkBox chunkBox = regionSlice.GetChunkBox();
                        ChunkPosition origin = chunkBox.Origin;
                        ChunkPosition max = chunkBox.Max;

                        for (int y = origin.Y; y < max.Y; y++)
                        {
                            for (int z = origin.Z; z < max.Z; z++)
                        {
                                for (int x = origin.X; x < max.X; x++)
                            {
                                    ChunkPosition position = new(x, y, z);
                                    
                                    AddChunk(region, position);
                                }
                            }
                        }
                    }

                    while (true)
                    {
                        currentPosition = dim.PlayerChunkPosition;

                        if (previousPosition == currentPosition)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        currentOrigin = currentPosition - centerOffsetMin;
                        currentMax = currentPosition + centerOffsetMax;
                        ChunkBox currentBox = new(currentOrigin, currentMax);

                        ChunkPosition previousOrigin = previousPosition - centerOffset;
                        ChunkPosition previousMax = previousPosition + centerOffset;
                        ChunkBox previousBox = new(previousOrigin, previousMax);

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(previousOrigin, previousMax))
                        {
                            using ValueArc<ChunkRegion> regionArc = dim.GetRegion(regionSlice.Region);
                            if (!regionArc.TryGet(out ChunkRegion? region))
                            {
                                continue;
                            }

                            ChunkBox chunkBox = regionSlice.GetChunkBox();
                            ChunkPosition origin = chunkBox.Origin;
                            ChunkPosition max = chunkBox.Max;

                            for (int y = origin.Y; y < max.Y; y++)
                            {
                                for (int z = origin.Z; z < max.Z; z++)
                            {
                                    for (int x = origin.X; x < max.X; x++)
                                {
                                        ChunkPosition position = new(x, y, z);
                                        if (currentBox.Contains(position))
                                        {
                                            region.RemoveChunk(position);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                        {
                            using ValueArc<ChunkRegion> regionArc = dim.CreateRegion(regionSlice.Region);
                            if (!regionArc.TryGet(out ChunkRegion? region))
                            {
                                continue;
                            }

                            ChunkBox chunkBox = regionSlice.GetChunkBox();
                            ChunkPosition origin = chunkBox.Origin;
                            ChunkPosition max = chunkBox.Max;

                            for (int y = origin.Y; y < max.Y; y++)
                            {
                                for (int z = origin.Z; z < max.Z; z++)
                                {
                                    for (int x = origin.X; x < max.X; x++)
                                    {
                                        ChunkPosition position = new(x, y, z);
                                        if (previousBox.Contains(position))
                                        {
                                            continue;
                                        }
                                        
                                        AddChunk(region, position);
                                    }
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
