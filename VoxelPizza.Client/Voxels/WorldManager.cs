using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            Dimension dimension = new();
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

                    int width = 40;
                    int depth = width;
                    int height = 40;

                    Size3 size = new((uint)width, (uint)height, (uint)depth);

                    ChunkPosition currentPosition = dimension.PlayerChunkPosition;
                    ChunkPosition previousPosition = currentPosition;

                    ChunkPosition centerOffset = new(width / 2, height / 2, depth / 2);

                    HashSet<ChunkPosition> currentChunks = new();

                    bool[,] loads = new bool[width, depth];

                    void AddChunk(ChunkRegion region, ChunkPosition position)
                    {
                        //bool a = currentChunks.Add(position);
                        //Debug.Assert(a);

                        if (position.Y == 0)
                        {
                            //ChunkPosition co = currentPosition - centerOffset;
                            //if (position.Y == 0)
                            //    loads[position.X - co.X, position.Z - co.Z] = true;

                            region.CreateChunk(position, out Chunk? chunk);
                            if (chunk != null)
                            {
                                try
                                {
                                    if (!chunk.Generate())
                                    {
                                        return;
                                    }
                                    //chunk.SetBlockLayer(0, 10);
                                    chunk.InvokeUpdate();
                                }
                                finally
                                {
                                    chunk.DecrementRef();
                                }
                            }
                        }
                    }

                    ChunkPosition currentOrigin = currentPosition - centerOffset;
                    ChunkPosition currentMax = currentPosition + centerOffset;

                    foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                    {
                        ChunkRegion region = dimension.CreateRegion(regionSlice.Region);
                        try
                        {
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
                                        AddChunk(region, position);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            region.DecrementRef();
                        }
                    }

                    //using var fs = new FileStream("what.txt", FileMode.Create);

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

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(previousOrigin, previousMax))
                        {
                            ChunkRegion? region = dimension.GetRegion(regionSlice.Region);
                            if (region == null)
                            {
                                continue;
                            }

                            try
                            {
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
                                                //if (position.Y == 0)
                                                //    loads[position.X - previousOrigin.X, position.Z - previousOrigin.Z] = false;

                                                region.RemoveChunk(position);
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                region.DecrementRef();
                            }
                        }

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                        {
                            ChunkRegion region = dimension.CreateRegion(regionSlice.Region);
                            try
                            {
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
                                                AddChunk(region, position);
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                region.DecrementRef();
                            }
                        }

                        previousPosition = currentPosition;

                        //int ww = currentMax.X - currentOrigin.X;
                        //int hh = currentMax.Y - currentOrigin.Y;
                        //int dd = currentMax.Z - currentOrigin.Z;
                        //using var sw = new StreamWriter(fs, System.Text.Encoding.UTF8, -1, true);
                        //sw.WriteLine(ww + " x " + hh + " x " + dd);
                        //
                        //ChunkPosition brorigin = currentOrigin;
                        //brorigin.Y = 0;
                        //for (int z = 0; z < depth; z++)
                        //{
                        //    for (int x = 0; x < width; x++)
                        //    {
                        //        bool load = loads[x, z];
                        //        Chunk? chunk = dimension.GetChunk(new ChunkPosition(x, 0, z) + brorigin);
                        //        bool hasChunk = chunk != null;
                        //        chunk?.DecrementRef();
                        //        sw.Write(load ? (hasChunk ? 2 : 1) : (hasChunk ? 4 : 0));
                        //    }
                        //    sw.WriteLine();
                        //}
                        //sw.WriteLine();
                    }

                    return;

                    List<(int x, int y, int z)>? list = new();

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
                    foreach ((int x, int y, int z) in list)
                    {
                        dimension.CreateChunk(new(x, y, z), out Chunk? chunk);
                        if (chunk == null)
                            continue;

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
                        foreach ((int x, int y, int z) in list)
                        {
                            Chunk? chunk = dimension.GetChunk(new(x, y, z));
                            if (chunk != null)
                            {
                                if (y >= 0 && x >= off && z >= off &&
                                    x < width - off &&
                                    z < depth - off)
                                {
                                    chunk.InvokeUpdate();
                                }
                                chunk.DecrementRef();
                            }
                        }
                    }

                    if (false)
                    {
                        Random rng = new(1234);
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
                                        Chunk? c = dimension.GetChunk(new ChunkPosition(x, y, z));
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

                                            c.DecrementRef();
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
                        Random rng = new(1234);
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
