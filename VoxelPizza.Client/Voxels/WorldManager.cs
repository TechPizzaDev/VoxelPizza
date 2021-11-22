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
            return dimension;
        }

        public Task CreateTestWorld(Dimension dimension, bool async)
        {
            void action()
            {
                try
                {
                    if (async)
                        Thread.Sleep(1000);

                    int width = 48;
                    int depth = width;
                    int height = 32;

                    ChunkPosition lastPos = new ChunkPosition(0, 0, 0);
                    HashSet<ChunkPosition> allChunks = new();
                    HashSet<ChunkPosition> newChunks = new();
                    HashSet<ChunkPosition> currentChunks = new();

                    while (true)
                    {
                        if (lastPos == dimension.PlayerChunkPosition)
                        {
                            Thread.Sleep(1);
                            continue;
                        }
                        lastPos = dimension.PlayerChunkPosition;

                        for (int y = 0; y < height; y++)
                        {
                            for (int z = 0; z < depth; z++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    var pos = new ChunkPosition(
                                        x + lastPos.X - width / 2,
                                        y + lastPos.Y - height / 2,
                                        z + lastPos.Z - depth / 2);

                                    if (allChunks.Add(pos))
                                        newChunks.Add(pos);

                                    currentChunks.Add(pos);
                                }
                            }
                        }

                        foreach (ChunkPosition pos in newChunks)
                        {
                            Chunk chunk = dimension.CreateChunk(pos);
                            try
                            {
                                if (pos.Y >= 0 && pos.Y <= 3)
                                {
                                    chunk.Generate();
                                    //chunk.SetBlockLayer(0, 10);
                                    chunk.InvokeUpdate();
                                }
                                else
                                {
                                    //chunk.SetBlockLayer(15, 1);
                                }
                            }
                            finally
                            {
                                chunk.DecrementRef();
                            }
                        }
                        newChunks.Clear();

                        foreach (ChunkPosition pos in allChunks)
                        {
                            if (!currentChunks.Contains(pos))
                            {
                                allChunks.Remove(pos);

                                dimension.RemoveChunk(pos);
                            }
                        }
                        currentChunks.Clear();

                    }
                    return;

                    var list = new List<(int x, int y, int z)>();

                    int off = 0;
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = -1; y < height; y++)
                        {
                            for (int z = 0; z < depth; z++)
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

                        if (y >= 0 && x >= off && z >= off &&
                            x < width - off &&
                            z < depth - off)
                        {
                            chunk?.InvokeUpdate();
                        }

                        count++;
                        if (count == width * height)
                        {
                            Thread.Sleep(1000);
                            count = 0;
                        }
                    }

                    if (false)
                    {
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

                    if (false)
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
            }

            if (async)
            {
                return Task.Run(action);
            }
            else
            {
                action();
                return Task.CompletedTask;
            }
        }
    }
}
