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
        public bool LoadChunks = true;

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
            //TerrainGenerator generator = new FillTerrainGenerator();

            Dimension dim = dimension.Get();

            void action()
            {
                try
                {
                    if (async)
                        Thread.Sleep(1000);
                    
                    int width = 64;
                    int depth = width;
                    int height = (int)(width * 1.33);

                    Size3 size = new((uint)width, (uint)height, (uint)depth);
                    ChunkTracker tracker = new(dimension, size);

                    tracker.TerrainGenerator = generator;

                    while (true)
                    {
                        if (!LoadChunks)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        tracker.Update(dim.PlayerChunkPosition);
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
