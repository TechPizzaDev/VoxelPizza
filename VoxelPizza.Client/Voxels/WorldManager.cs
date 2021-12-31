using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

                    Size3 size = new((uint)width, (uint)height, (uint)depth);

                    ChunkPosition currentPosition = dimension.PlayerChunkPosition;
                    ChunkPosition previousPosition = currentPosition;

                    ChunkPosition centerOffset = new(width / 2, height / 2, depth / 2);

                    HashSet<ChunkPosition> currentChunks = new();

                    void AddChunk(ChunkPosition position)
                    {
                        //bool a = currentChunks.Add(position);
                        //Debug.Assert(a);

                        if (position.Y >= -1 && position.Y <= 1)
                        {
                            Chunk chunk = dimension.CreateChunk(position);
                            try
                            {
                                //chunk.Generate();
                                chunk.SetBlockLayer(0, 10);
                                chunk.InvokeUpdate();
                            }
                            finally
                            {
                                chunk.DecrementRef();
                            }
                        }
                    }

                    ChunkPosition currentOrigin = currentPosition - centerOffset;
                    ChunkPosition currentMax = currentPosition + centerOffset;

                    ChunkPosition position;
                    for (position.Y = currentOrigin.Y; position.Y < currentMax.Y; position.Y++)
                    {
                        for (position.Z = currentOrigin.Z; position.Z < currentMax.Z; position.Z++)
                        {
                            for (position.X = currentOrigin.X; position.X < currentMax.X; position.X++)
                            {
                                AddChunk(position);
                            }
                        }
                    }

                    while (true)
                    {
                        currentPosition = dimension.PlayerChunkPosition;

                        if (previousPosition == currentPosition)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        currentOrigin = currentPosition - centerOffset;
                        currentMax = currentPosition + centerOffset;
                        ChunkBox currentBox = new(currentOrigin, currentMax);

                        ChunkPosition previousOrigin = previousPosition - centerOffset;
                        ChunkPosition previousMax = previousPosition + centerOffset;
                        ChunkBox previousBox = new(previousOrigin, previousMax);

                        for (position.Y = previousOrigin.Y; position.Y < previousMax.Y; position.Y++)
                        {
                            for (position.Z = previousOrigin.Z; position.Z < previousMax.Z; position.Z++)
                            {
                                for (position.X = previousOrigin.X; position.X < previousMax.X; position.X++)
                                {
                                    if (!currentBox.Contains(position))
                                    {
                                        dimension.RemoveChunk(position);
                                    }
                                }
                            }
                        }

                        for (position.Y = currentOrigin.Y; position.Y < currentMax.Y; position.Y++)
                        {
                            for (position.Z = currentOrigin.Z; position.Z < currentMax.Z; position.Z++)
                            {
                                for (position.X = currentOrigin.X; position.X < currentMax.X; position.X++)
                                {
                                    if (!previousBox.Contains(position))
                                    {
                                        AddChunk(position);
                                    }
                                }
                            }
                        }

                        previousPosition = currentPosition;
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
                                            //uint[] blocks = c.Blocks;
                                            //for (int b = 0; b < blocks.Length; b++)
                                            //{
                                            //    blocks[b] = (uint)rng.Next(max);
                                            //}
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
                                        c.SetBlock((nuint)rng.Next(16 * 16 * 16), (uint)rng.Next(128));

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
