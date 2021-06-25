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
                    Thread.Sleep(2000);

                    int width = 16;
                    int depth = width;
                    int height = 1;

                    var list = new List<(int x, int y, int z)>();

                    int off = 0;
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

                        //ChunkRegionPosition regionPosition = GetRegionPosition(chunk.Position);
                        //ChunkMeshRegion? region;
                        //lock (_regions)
                        //{
                        //    if (!_regions.TryGetValue(regionPosition, out region))
                        //    {
                        //        region = new ChunkMeshRegion(this, regionPosition, RegionSize);
                        //        _regions.Add(regionPosition, region);
                        //        _queuedRegions.Enqueue(region);
                        //    }
                        //}

                        //region.UpdateChunk(chunk);

                        //var mesh = new ChunkMesh(this, chunk);
                        //_queuedMeshes.Enqueue(mesh);

                        count++;
                        if (count == 1)
                        {
                            //Thread.Sleep(1);
                            count = 0;
                        }
                    }

                    //for (int y = 0; y < height; y++)
                    //{
                    //    for (int z = off; z < depth - off; z++)
                    //    {
                    //        for (int x = off; x < width - off; x++)
                    //        {
                    //            var pp = new ChunkPosition(x, y, z);
                    //            ChunkRegionPosition regionPosition = GetRegionPosition(pp);
                    //            Chunk? chunk = GetChunk(pp);
                    //            _regions[regionPosition].UpdateChunk(chunk);
                    //        }
                    //    }
                    //}

                    return;

                    //Random rng = new Random(1234);
                    //for (int i = 0; i < (64 * 1024) / (width * height * depth); i++)
                    //{
                    //    for (int y = 0; y < height; y++)
                    //    {
                    //        for (int z = 0; z < depth; z++)
                    //        {
                    //            for (int x = 0; x < width; x++)
                    //            {
                    //                int xd = rng.Next(2);
                    //                int max = xd == 0 ? 512 : 128;
                    //                if (_chunks.TryGetValue(new ChunkPosition(x, y, z), out Chunk? c))
                    //                {
                    //                    uint[] blocks = c.Blocks;
                    //                    for (int b = 0; b < blocks.Length; b++)
                    //                    {
                    //                        blocks[b] = (uint)rng.Next(max);
                    //                    }
                    //                    //blocks.AsSpan().Clear();
                    //
                    //                    ChunkRegionPosition regionPosition = GetRegionPosition(c.Position);
                    //                    _regions[regionPosition].UpdateChunk(c);
                    //                }
                    //            }
                    //        }
                    //    }
                    //
                    //    frameEvent.WaitOne();
                    //    frameEvent.WaitOne();
                    //    //frameEvent.WaitOne();
                    //    //frameEvent.WaitOne();
                    //}
                    //
                    //frameEvent.WaitOne();
                    //Thread.Sleep(1000);
                    //Environment.Exit(0);
                    //
                    //return;
                    //Thread.Sleep(5000);
                    //
                    //while (true)
                    //{
                    //    int x = rng.Next(width);
                    //    int z = rng.Next(depth);
                    //    int y = rng.Next(height);
                    //    if (_chunks.TryGetValue(new ChunkPosition(x, y, z), out Chunk? c))
                    //    {
                    //        c.Blocks[rng.Next(c.Blocks.Length)] = 0;
                    //
                    //        ChunkRegionPosition regionPosition = GetRegionPosition(c.Position);
                    //        _regions[regionPosition].UpdateChunk(c);
                    //
                    //        Thread.Sleep(10);
                    //    }
                    //}
                }   
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }
    }
}
