using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            Dimension dimension = new Dimension();
            CreateTestWorld(dimension);
            return dimension;
        }

        private Task CreateTestWorld(Dimension dimension)
        {
            return Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(1000);

                    int width = 64;
                    int depth = width;
                    int height = 4;

                    var list = new List<(int x, int y, int z)>();

                    int off = 1;
                    for (int y = -off; y < height; y++)
                    {
                        for (int z = 0; z < depth; z++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                list.Add((x, y, z));
                            }
                        }
                    }

                    list.Sort((a, b) =>
                    {
                        int cx = width / 2;
                        int cy = height / 2;
                        int cz = depth / 2;

                        int ax = (cx - a.x);
                        int ay = (cy - a.y);
                        int az = (cz - a.z);
                        int av = ax * ax + ay * ay + az * az;

                        int bx = (cx - b.x);
                        int by = (cy - b.y);
                        int bz = (cz - b.z);
                        int bv = bx * bx + by * by + bz * bz;

                        return av.CompareTo(bv);
                    });

                    int count = 0;
                    foreach (var (x, y, z) in list)
                    {
                        Chunk chunk = dimension.CreateChunk(new(x, y, z));

                        if (y < 0)
                            chunk.SetBlockLayer(15, 1);
                        else
                            chunk.Generate();

                        //if (y >= 0 && x >= off && z >= off &&
                        //    x < width - off &&
                        //    z < depth - off)
                        //{
                        //    chunk.InvokeUpdate();
                        //}

                        count++;
                        if (count == 1)
                        {
                            //Thread.Sleep(1);
                            count = 0;
                        }
                    }

                    foreach (var (x, y, z) in list)
                    {
                        Chunk? chunk = dimension.GetChunk(new(x, y, z));

                        if (y >= 0 && x >= off && z >= off &&
                            x < width - off &&
                            z < depth - off)
                        {
                            chunk?.InvokeUpdate();
                        }
                    }

                    if (false)
                    {
                        Random rng = new Random(1234);
                        for (int i = 0; i < (64 * 1024) / (width * height * depth); i++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                for (int z = 0; z < depth; z++)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        int xd = rng.Next(2);
                                        int max = xd == 0 ? 512 : 128;
                                        var c = dimension.GetChunk(new ChunkPosition(x, y, z));
                                        if (c != null)
                                        {
                                            uint[] blocks = c.Blocks;
                                            for (int b = 0; b < blocks.Length; b++)
                                            {
                                                blocks[b] = (uint)rng.Next(max);
                                            }
                                            //blocks.AsSpan().Clear();


                                            //ChunkRegionPosition regionPosition = GetRegionPosition(c.Position);
                                            //_regions[regionPosition].UpdateChunk(c);
                                        }
                                    }
                                }
                            }
                            //frameEvent.WaitOne();
                            //frameEvent.WaitOne();
                            //frameEvent.WaitOne();
                            //frameEvent.WaitOne();
                        }

                        //frameEvent.WaitOne();
                        Thread.Sleep(1000);
                        Environment.Exit(0);
                    }

                    if (true)
                    {
                        Random rng = new Random(1234);
                        while (true)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                int x = rng.Next(width - off * 2) + off;
                                int z = rng.Next(depth - off * 2) + off;
                                int y = rng.Next(height);
                                Chunk? c = dimension.GetChunk(new ChunkPosition(x, y, z));
                                if (c != null)
                                {
                                    try
                                    {
                                        c.SetBlock(rng.Next(16 * 16 * 16), (uint)rng.Next(128));

                                        c.InvokeUpdate();
                                    }
                                    finally
                                    {
                                        c.DecrementRef();
                                    }
                                }
                            }

                            Thread.Sleep(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }
    }
}
