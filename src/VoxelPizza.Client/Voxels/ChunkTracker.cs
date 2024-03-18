using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client;

public class ChunkTracker
{
    private ValueArc<Dimension> _dimension;

    public Size3 TrackingSize { get; }

    public TerrainGenerator TerrainGenerator { get; set; }

    private ChunkBox _previousChunkBox;

    private Stopwatch _workWatch = new();

    private ConcurrentDictionary<ChunkRegionPosition, ChunkTicketRegion> _ticketRegions = new();

    private SortedDictionary<uint, List<TicketEntry>> _ticketLists = new();

    private List<TicketEntry> _filteredTickets = new();

    private List<ChunkTicket> _cancelledTickets = new();

    public ChunkTracker(ValueArc<Dimension> dimension, Size3 trackingSize)
    {
        _dimension = dimension.Track();
        TrackingSize = trackingSize;
    }

    public ValueArc<Dimension> GetDimension()
    {
        return _dimension.Wrap();
    }

    private ChunkPosition GetCenterOffsetMin()
    {
        return new(
            (int)(TrackingSize.W / 2),
            (int)(TrackingSize.H / 2),
            (int)(TrackingSize.D / 2));
    }

    private ChunkPosition GetCenterOffsetMax()
    {
        return new(
            (int)((TrackingSize.W + 1) / 2),
            (int)((TrackingSize.H + 1) / 2),
            (int)((TrackingSize.D + 1) / 2));
    }

    public void Update(ChunkPosition currentPosition)
    {
        ChunkPosition centerOffsetMin = GetCenterOffsetMin();
        ChunkPosition centerOffsetMax = GetCenterOffsetMax();

        ChunkPosition currentOrigin = currentPosition - centerOffsetMin;
        ChunkPosition currentMax = currentPosition + centerOffsetMax;
        ChunkBox currentBox = new(currentOrigin, currentMax);

        bool moved = _previousChunkBox.Origin != currentBox.Origin || _previousChunkBox.Size.IsZero;

        ProcessTickets();

        if (moved)
        {
            _workWatch.Restart();
            RemoveTickets(currentBox, _previousChunkBox);
            _workWatch.Stop();
            Console.WriteLine($"Removed tickets in {_workWatch.Elapsed.TotalMilliseconds:0.00}ms");
            _workWatch.Restart();
            ProcessCancelledTickets();
            _workWatch.Stop();
            Console.WriteLine($"Processed cancelled tickets in {_workWatch.Elapsed.TotalMilliseconds:0.000}ms");

            _workWatch.Restart();
            AddTickets(currentBox, _previousChunkBox);
            _workWatch.Stop();
            Console.WriteLine($"Added tickets in {_workWatch.Elapsed.TotalMilliseconds:0.00}ms");
            _workWatch.Restart();
            ProcessCancelledTickets();
            _workWatch.Stop();
            Console.WriteLine($"Processed cancelled tickets in {_workWatch.Elapsed.TotalMilliseconds:0.000}ms");

            GatherTickets(currentPosition);
            ProcessCancelledTickets();
        }

        _previousChunkBox = currentBox;
    }

    private static uint GetDistance(BlockPosition left, BlockPosition right)
    {
        uint dx = (uint)IntMath.Abs(right.X - left.X);
        uint dy = (uint)IntMath.Abs(right.Y - left.Y);
        uint dz = (uint)IntMath.Abs(right.Z - left.Z);
        return dx + dy + dz;
    }

    private void GatherTickets(ChunkPosition currentPosition)
    {
        BlockPosition chunkOffset = new(Chunk.Width / 2, Chunk.Height / 2, Chunk.Depth / 2);

        _workWatch.Restart();

        foreach (var list in _ticketLists.Values)
        {
            list.Clear();
        }

        foreach ((ChunkRegionPosition pos, ChunkTicketRegion ticketRegion) in _ticketRegions)
        {
            if (ticketRegion.Count == 0)
            {
                _ticketRegions.TryRemove(ticketRegion.Position, out _);
                continue;
            }

            BlockPosition currentPos = currentPosition.ToBlock() - chunkOffset;

            foreach (ChunkTicket? ticket in ticketRegion.AsSpan())
            {
                if (ticket == null)
                {
                    continue;
                }

                Debug.Assert(!ticket.IsStopRequested);

                ChunkPosition chunkPos = ticket.GetChunk().Get().Position;
                uint dist = GetDistance(chunkPos.ToBlock(), currentPos);

                if (!_ticketLists.TryGetValue(dist, out var list))
                {
                    list = new();
                    _ticketLists.Add(dist, list);
                }

                list.Add(new(ticketRegion, chunkPos, ticket));
            }
        }

        _workWatch.Stop();

        int ticketListCount = _ticketLists.Values.Count;
        int ticketCount = _ticketLists.Values.Sum(x => x.Count);

        if (ticketListCount > 0 || ticketCount > 0)
        {
            Console.WriteLine(
                $"Gather Chunk tickets ({_workWatch.Elapsed.TotalMilliseconds:0.00}ms): " +
                $"List count {ticketListCount}, Ticket count {ticketCount}");
        }
    }

    private void ProcessTickets()
    {
        int completedCount = 0;
        int processedCount = 0;
        int cancelledCount = 0;
        int stoppedCount = 0;

        _workWatch.Restart();

        bool timeExhausted = false;

        foreach ((uint listKey, List<TicketEntry> ticketList) in _ticketLists)
        {
            if (ticketList.Count == 0)
            {
                continue;
            }

            Span<TicketEntry> ticketSpan = CollectionsMarshal.AsSpan(ticketList);
            for (int i = 0; i < ticketSpan.Length; i++)
            {
                if (_workWatch.Elapsed.TotalMilliseconds > 25)
                {
                    ticketSpan = ticketSpan.Slice(0, i);
                    timeExhausted = true;
                    break;
                }

                ref readonly TicketEntry entry = ref ticketSpan[i];
                ChunkTicket ticket = entry.Ticket;

                bool remove = false;
                if (!ticket.IsStopRequested)
                {
                    ticket.Work(GeneratorState.Complete);
                    if (ticket.State == GeneratorState.Complete)
                    {
                        ticket.GetChunk().Get().InvokeUpdate();

                        Interlocked.Increment(ref completedCount);
                        remove = true;
                    }
                    Interlocked.Increment(ref processedCount);
                }
                else
                {
                    Interlocked.Increment(ref stoppedCount);
                    remove = true;
                }

                if (remove && entry.TicketRegion.Remove(entry.Position, out ChunkTicket? removedTicket))
                {
                    Debug.Assert(removedTicket == ticket);

                    GeneratorState prevState = ticket.Work(GeneratorState.Dispose);
                    Debug.Assert(ticket.State == GeneratorState.Dispose);

                    if (prevState == GeneratorState.Cancel)
                    {
                        Interlocked.Increment(ref cancelledCount);
                    }
                }
            }

            int removedTickets = 0;
            for (int i = 0; i < ticketSpan.Length; i++)
            {
                ChunkTicket? ticket = ticketSpan[i].Ticket;
                if (ticket.IsStopRequested)
                {
                    Debug.Assert(ticket.State == GeneratorState.Dispose);
                    removedTickets++;
                }
                else
                {
                    _filteredTickets.Add(ticketSpan[i]);
                }
            }

            if (removedTickets != 0)
            {
                // Append all untouched tickets at the end of the filtered list.
                _filteredTickets.AddRange(CollectionsMarshal.AsSpan(ticketList).Slice(ticketSpan.Length));

                ticketList.Clear();
                ticketList.AddRange(_filteredTickets);
            }

            _filteredTickets.Clear();

            if (timeExhausted)
            {
                break;
            }
        }

        _workWatch.Stop();

        if (cancelledCount > 0 || processedCount > 0 || completedCount > 0 || stoppedCount > 0)
        {
            Console.WriteLine(
                $"Chunk tickets ({_workWatch.Elapsed.TotalMilliseconds:0.00}ms): " +
                $"Completed {completedCount}, Processed {processedCount}, Cancelled {cancelledCount}, Stopped {stoppedCount}");
        }

        if (!timeExhausted)
        {
            Thread.Sleep(1);
        }
    }

    private void RemoveTickets(ChunkBox currentBox, ChunkBox previousBox)
    {
        Dimension dim = _dimension.Get();

        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(previousBox.Origin, previousBox.Max))
        {
            using ValueArc<ChunkRegion> regionArc = dim.GetRegion(regionSlice.Region);
            if (!regionArc.TryGet(out ChunkRegion? region))
            {
                continue;
            }

            _ticketRegions.TryGetValue(region.Position, out ChunkTicketRegion? ticketRegion);

            ChunkBox chunkBox = regionSlice.GetChunkBox();
            ChunkPosition origin = chunkBox.Origin;
            ChunkPosition max = chunkBox.Max;

            // TODO: batch-locking the region
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

                        if (ticketRegion != null && ticketRegion.Remove(position, out ChunkTicket? ticket))
                        {
                            ticket.Work(GeneratorState.Cancel);
                            _cancelledTickets.Add(ticket);
                        }
                    }
                }
            }
        }
    }

    private void AddTickets(ChunkBox currentBox, ChunkBox previousBox)
    {
        foreach (ChunkRegionBoxSlice regionSlice in new ChunkRegionBoxSliceEnumerator(currentBox.Origin, currentBox.Max))
        {
            using ValueArc<ChunkRegion> regionArc = Dimension.CreateRegion(_dimension, regionSlice.Region);
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
                            if (!_ticketRegions.TryGetValue(region.Position, out ticketRegion))
                            {
                                ticketRegion = new ChunkTicketRegion(region.Position);
                                _ticketRegions.TryAdd(region.Position, ticketRegion);
                            }
                        }

                        if (ticketRegion.Remove(position, out ChunkTicket? existingTicket))
                        {
                            existingTicket.Work(GeneratorState.Cancel);
                            _cancelledTickets.Add(existingTicket);
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
    }

    private ChunkTicket? AddChunk(ValueArc<ChunkRegion> chunkRegion, ChunkPosition position)
    {
        if (!TerrainGenerator.CanGenerate(position))
        {
            return null;
        }

        ChunkTicket? ticket = TerrainGenerator.CreateTicket(chunkRegion, position);
        return ticket;
    }

    private void ProcessCancelledTickets()
    {
        if (_cancelledTickets.Count <= 0)
        {
            return;
        }

        foreach (ChunkTicket ticket in _cancelledTickets)
        {
            ticket.Work(GeneratorState.Dispose);
        }

        //Console.WriteLine($"Cleared {cancelledTickets.Count} cancelled chunks");

        _cancelledTickets.Clear();
    }
}

internal readonly struct TicketEntry(ChunkTicketRegion ticketRegion, ChunkPosition position, ChunkTicket ticket)
{
    public ChunkTicketRegion TicketRegion { get; } = ticketRegion;
    public ChunkPosition Position { get; } = position;
    public ChunkTicket Ticket { get; } = ticket;
}