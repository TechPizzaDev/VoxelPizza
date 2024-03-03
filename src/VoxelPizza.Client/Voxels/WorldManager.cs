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

            Dimension dim = dimension.Get();

            void action()
            {
                try
                {
                    if (async)
                        Thread.Sleep(1000);

                    int width = 64;
                    int depth = width;
                    int height = (int)(width * 1.5);

                    Size3 size = new((uint)width, (uint)height, (uint)depth);

                    ChunkPosition currentPosition = dim.PlayerChunkPosition;
                    ChunkPosition previousPosition = currentPosition;

                    ChunkPosition centerOffsetMin = new(width / 2, height / 2, depth / 2);
                    ChunkPosition centerOffsetMax = new((width + 1) / 2, (height + 1) / 2, (depth + 1) / 2);

                    ChunkTicket? AddChunk(ValueArc<ChunkRegion> chunkRegion, ChunkPosition position)
                    {
                        if (!generator.CanGenerate(position))
                        {
                            return null;
                        }

                        ChunkTicket? ticket = generator.CreateTicket(chunkRegion, position);
                        return ticket;
                    }

                    ChunkPosition currentOrigin = currentPosition - centerOffsetMin;
                    ChunkPosition currentMax = currentPosition + centerOffsetMax;

                    Dictionary<ChunkRegionPosition, ChunkTicketRegion> ticketRegions = new();

                    foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                    {
                        using ValueArc<ChunkRegion> regionArc = Dimension.CreateRegion(dimension, regionSlice.Region);
                        if (!regionArc.TryGet(out ChunkRegion? region))
                        {
                            continue;
                        }

                        ChunkTicketRegion? ticketRegion = null;

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

                                    ChunkTicket? ticket = AddChunk(regionArc, position);
                                    if (ticket == null)
                                    {
                                        continue;
                                    }

                                    if (ticketRegion == null)
                                    {
                                        if (!ticketRegions.TryGetValue(region.Position, out ticketRegion))
                                        {
                                            ticketRegion = new ChunkTicketRegion(region.Position);
                                            ticketRegions.Add(region.Position, ticketRegion);
                                        }
                                    }
                                    ticketRegion.Add(position, ticket);
                                }
                            }
                        }
                    }

                    Stopwatch workWatch = new();

                    List<ChunkTicket> cancelledTickets = new();

                    while (true)
                    {
                        currentPosition = dim.PlayerChunkPosition;

                        if (previousPosition == currentPosition)
                        {
                            workWatch.Restart();

                            int skipped = 0;
                            bool timeExhausted = false;

                            foreach (ChunkTicketRegion ticketRegion in ticketRegions.Values)
                            {
                                if (ticketRegion.Count == 0)
                                {
                                    ticketRegions.Remove(ticketRegion.Position);
                                    continue;
                                }

                                foreach (ChunkTicket? ticket in ticketRegion.AsSpan())
                                {
                                    if (ticket == null)
                                    {
                                        continue;
                                    }

                                    if (workWatch.Elapsed.TotalMilliseconds > 10)
                                    {
                                        timeExhausted = true;
                                        break;
                                    }

                                    Chunk chunk = ticket.GetChunk().Get();

                                    if (!ticket.IsStopRequested)
                                    {
                                        GeneratorState prevState = ticket.Work(GeneratorState.Complete);
                                        if (ticket.State == GeneratorState.Complete)
                                        {
                                            chunk.InvokeUpdate();
                                        }
                                    }

                                    if (ticketRegion.Remove(chunk.Position, out _))
                                    {
                                        if (ticket.State == GeneratorState.Cancel)
                                            skipped++;

                                        ticket.Work(GeneratorState.Dispose);
                                    }
                                }
                            }
                            workWatch.Stop();

                            if (skipped > 0)
                            {
                                //Console.WriteLine($"Skipped {skipped} cancelled chunks");
                            }

                            if (!timeExhausted)
                            {
                                Thread.Sleep(1);
                            }
                            continue;
                        }

                        currentOrigin = currentPosition - centerOffsetMin;
                        currentMax = currentPosition + centerOffsetMax;
                        ChunkBox currentBox = new(currentOrigin, currentMax);

                        ChunkPosition previousOrigin = previousPosition - centerOffsetMin;
                        ChunkPosition previousMax = previousPosition + centerOffsetMax;
                        ChunkBox previousBox = new(previousOrigin, previousMax);

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(previousOrigin, previousMax))
                        {
                            using ValueArc<ChunkRegion> regionArc = dim.GetRegion(regionSlice.Region);
                            if (!regionArc.TryGet(out ChunkRegion? region))
                            {
                                continue;
                            }

                            ticketRegions.TryGetValue(region.Position, out ChunkTicketRegion? ticketRegion);

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
                                            continue;
                                        }

                                        region.RemoveChunk(position);

                                        if (ticketRegion != null && ticketRegion.TryGet(position, out ChunkTicket? ticket))
                                        {
                                            ticket.Work(GeneratorState.Cancel);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentOrigin, currentMax))
                        {
                            using ValueArc<ChunkRegion> regionArc = Dimension.CreateRegion(dimension, regionSlice.Region);
                            if (!regionArc.TryGet(out ChunkRegion? region))
                            {
                                continue;
                            }

                            ChunkTicketRegion? ticketRegion = null;

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

                                        if (ticketRegion == null)
                                        {
                                            if (!ticketRegions.TryGetValue(region.Position, out ticketRegion))
                                            {
                                                ticketRegion = new ChunkTicketRegion(region.Position);
                                                ticketRegions.Add(region.Position, ticketRegion);
                                            }
                                        }

                                        if (ticketRegion.Remove(position, out ChunkTicket? existingTicket))
                                        {
                                            cancelledTickets.Add(existingTicket);
                                        }

                                        ChunkTicket? ticket = AddChunk(regionArc, position);
                                        if (ticket != null)
                                        {
                                            ticketRegion.Add(position, ticket);
                                        }
                                    }
                                }
                            }
                        }

                        if (cancelledTickets.Count > 0)
                        {
                            //Console.WriteLine($"Cleared {cancelledTickets.Count} cancelled chunks");
                        }

                        foreach (ChunkTicket ticket in cancelledTickets)
                        {
                            ticket.Work(GeneratorState.Dispose);
                        }
                        cancelledTickets.Clear();

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
