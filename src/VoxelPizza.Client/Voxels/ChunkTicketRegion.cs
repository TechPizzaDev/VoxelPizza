using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class ChunkTicketRegion
    {
        private ChunkRegionPosition _position;
        private ChunkTicket?[] _tickets;
        private int _count;

        public ChunkRegionPosition Position => _position;

        public int Count => _count;

        public ChunkTicketRegion(ChunkRegionPosition position)
        {
            _position = position;

            _tickets = new ChunkTicket[ChunkRegion.Size.Volume];
        }

        public void Add(ChunkPosition position, ChunkTicket ticket)
        {
            if (ticket == null)
                throw new ArgumentNullException(nameof(ticket));

            ChunkRegion.CheckChunkPosition(Position, position);

            ChunkPosition localPosition = ChunkRegion.GetLocalChunkPosition(position);
            int index = ChunkRegion.GetChunkIndex(localPosition);

            ChunkTicket?[] tickets = _tickets;
            ChunkTicket? previousTicket = Interlocked.Exchange(ref tickets[index], ticket);
            if (previousTicket != null)
            {
                throw new InvalidOperationException();
            }
            Interlocked.Increment(ref _count);
        }

        public bool TryGet(ChunkPosition position, [MaybeNullWhen(false)] out ChunkTicket ticket)
        {
            ChunkRegion.CheckChunkPosition(Position, position);

            ChunkPosition localPosition = ChunkRegion.GetLocalChunkPosition(position);
            int index = ChunkRegion.GetChunkIndex(localPosition);

            ticket = _tickets[index];
            return ticket != null;
        }

        public bool Contains(ChunkPosition position)
        {
            return TryGet(position, out _);
        }

        public bool Remove(ChunkPosition position, [MaybeNullWhen(false)] out ChunkTicket ticket)
        {
            ChunkRegion.CheckChunkPosition(Position, position);

            ChunkPosition localPosition = ChunkRegion.GetLocalChunkPosition(position);
            int index = ChunkRegion.GetChunkIndex(localPosition);

            ChunkTicket?[] tickets = _tickets;
            ticket = Interlocked.Exchange(ref tickets[index], null);
            if (ticket != null)
            {
                Interlocked.Decrement(ref _count);
                return true;
            }
            return false;
        }

        public Span<ChunkTicket?> AsSpan()
        {
            return _tickets.AsSpan();
        }
    }
}
