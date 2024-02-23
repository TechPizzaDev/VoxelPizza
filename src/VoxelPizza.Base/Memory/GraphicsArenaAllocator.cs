using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Veldrid;

namespace VoxelPizza.Memory
{
    public readonly struct GraphicsArenaAllocator
    {
        public DeviceBuffer Buffer { get; }
        public ArenaAllocator Allocator { get; }

        public BufferUsage Usage => Buffer.Usage;
        public uint ByteCapacity => Allocator.ElementCapacity;
        public uint SegmentsUsed => Allocator.SegmentsUsed;
        public uint SegmentsFree => Allocator.SegmentsFree;
        public uint BytesUsed => Allocator.ElementsUsed;
        public uint BytesFree => Allocator.ElementsFree;

        public GraphicsArenaAllocator(DeviceBuffer buffer, ArenaAllocator allocator)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        }

        public bool TryAlloc(uint size, uint alignment, out ArenaSegment segment)
        {
            return Allocator.TryAlloc(size, alignment, out segment);
        }

        public void Free(ArenaSegment segment)
        {
            Allocator.Free(segment);
        }

        public static GraphicsArenaAllocator Create(ResourceFactory factory, uint byteCapacity, BufferUsage usage)
        {
            BufferDescription description = new(byteCapacity, usage, 0, rawBuffer: true);
            DeviceBuffer buffer = factory.CreateBuffer(description);
            return new GraphicsArenaAllocator(buffer, new ArenaAllocator(byteCapacity));
        }

        public static GraphicsArenaAllocator Create<T>(ResourceFactory factory, uint elementCapacity, BufferUsage usage)
            where T : unmanaged
        {
            return Create(factory, elementCapacity * (uint)Unsafe.SizeOf<T>(), usage);
        }

        public static void Test(GraphicsDevice device)
        {
            BufferDescription desc = new(1024 * 1024, BufferUsage.VertexBuffer);

            GraphicsArenaAllocator a = new(
                device.ResourceFactory.CreateBuffer(desc),
                new ArenaAllocator(desc.SizeInBytes));

            static void Assert(bool condition)
            {
                Debug.Assert(condition);
            }

            for (int i = 0; i < (16 * 1024 * 1024) / 21; i++)
            {
                Assert(a.Allocator.TryAlloc(1024 * 512, 1, out ArenaSegment segmentXX1));
                Assert(segmentXX1.Length == 1024 * 512);

                Assert(a.Allocator.TryAlloc(1024 * 512, 1, out ArenaSegment segmentXX2));
                Assert(segmentXX2.Length == 1024 * 512);
                a.Allocator.Free(segmentXX2);
                a.Allocator.Free(segmentXX1);

                Assert(a.Allocator.TryAlloc(1024 * 512, 1, out ArenaSegment segment1));
                Assert(segment1.Length == 1024 * 512);

                Assert(a.Allocator.TryAlloc(1024 * 512, 1, out ArenaSegment segment2));
                Assert(segment2.Length == 1024 * 512);

                Assert(!a.Allocator.TryAlloc(1024 * 512, 1, out ArenaSegment _));

                a.Allocator.Free(segment2);
                a.Allocator.Free(segment1);

                Assert(a.Allocator.TryAlloc(1024 * 1024, 1, out ArenaSegment segment4));
                Assert(segment4.Length == 1024 * 1024);

                a.Allocator.Free(segment4);

                Assert(!a.Allocator.TryAlloc(1024 * 1024 + 1, 1, out ArenaSegment _));
                //Debug.Assert(allocator.UsedBytes == 0);

                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment6));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment7));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment8));

                a.Allocator.Free(segment6);
                a.Allocator.Free(segment8);
                a.Allocator.Free(segment7);
                //Debug.Assert(a.UsedBytes == 0);

                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment10));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment11));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment12));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment13));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment14));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment15));
                Assert(a.Allocator.TryAlloc(1024, 1, out ArenaSegment segment16));

                a.Allocator.Free(segment11);
                a.Allocator.Free(segment13);
                a.Allocator.Free(segment15);

                //using CommandList cl = device.ResourceFactory.CreateCommandList();
                //cl.Begin();
                //a.Resize(cl, 1024 * 4);
                //cl.End();
                //device.SubmitCommands(cl);
                //device.WaitForIdle();

                a.Allocator.Free(segment10);
                a.Allocator.Free(segment12);
                a.Allocator.Free(segment14);
                a.Allocator.Free(segment16);

                Assert(a.Allocator.TryAlloc(1024 * 4, 1, out ArenaSegment segment17));
                a.Allocator.Free(segment17);

                Assert(a.Allocator.TryAlloc(1024 * 4, 1, out ArenaSegment segment18));
                Assert(a.Allocator.TryAlloc(1024 * 4, 1, out ArenaSegment segment19));
                Assert(a.Allocator.TryAlloc(1024 * 4, 1, out ArenaSegment segment20));
                a.Allocator.Free(segment19);
                Assert(a.Allocator.TryAlloc(1024 * 4, 1, out ArenaSegment segment21));
                a.Allocator.Free(segment18);
                a.Allocator.Free(segment21);
                a.Allocator.Free(segment20);
            }
        }
    }
}
