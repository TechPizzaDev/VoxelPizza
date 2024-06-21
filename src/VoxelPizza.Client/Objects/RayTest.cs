using System;
using System.Numerics;
using VoxelPizza.Diagnostics;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class RayTest
    {
        public float time;
        public int index;

        public void Update(in UpdateState state, ValueArc<Dimension> dimension)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            for (int i = 0; i < 10; i++)
            {
                uint id = index % 2 == 0 ? 0u : 1;
                float t = index % 2 == 0 ? time : (time + MathF.PI);

                Vector3 dir = new(
                    MathF.Cos(t),
                    (MathF.Cos(t * 30) + 1) * 0.25f,
                    MathF.Sin(t));

                dir = Vector3.Normalize(dir);

                for (int y = 0; y < 1; y++)
                {
                    for (int z = 0; z < 1; z++)
                    {
                        for (int x = 0; x < 1; x++)
                        {
                            //using RefCounted<ChunkRegion?> countedRegion = dimension.GetRegion(new ChunkRegionPosition(x, y, z));
                            //if (countedRegion.TryGetValue(out ChunkRegion? region))
                            {
                                Vector3 origin =
                                    new Vector3(0, 0, 0)
                                    + new Vector3(0.5f);

                                VoxelRayCast rayCast = new(origin, dir);

                                const int distance = 300;
                                int blocksLeft = 4096;

                                using Dimension.BlockRayCast blockRay = new(dimension.Track());
                                var chunk = ValueArc<Chunk>.Empty;
                                bool changed = false;
                                BlockRayCastStatus status;
                                while ((status = blockRay.MoveNext(ref rayCast)) != BlockRayCastStatus.End)
                                {
                                    if (blocksLeft-- <= 0)
                                        break;

                                    Int3 current = rayCast.Current;
                                    if ((current - origin).LengthSquared() > distance * distance)
                                        break;

                                    if (status == BlockRayCastStatus.Chunk)
                                    {
                                        if (changed)
                                        {
                                            chunk.Get().InvokeUpdate();
                                            changed = false;
                                        }
                                        chunk.Dispose();

                                        chunk = blockRay.CurrentChunk.Track();
                                    }
                                    else if (status == BlockRayCastStatus.Block)
                                    {
                                        Chunk c = chunk.Get();
                                        Int3 p = current - c.Position.ToBlock().ToInt3();
                                        changed |= c.GetBlockStorage().SetBlock(p.X, p.Y, p.Z, id);
                                    }
                                }
                                if (changed)
                                {
                                    chunk.Get().InvokeUpdate();
                                }
                                chunk.Dispose();
                            }
                        }
                    }
                }

                index++;

                time += MathF.PI * 2 / 180 * 0.01f;
                if (time >= MathF.PI * 2)
                {
                    //id = (id + 1) % 2;
                    time -= MathF.PI * 2;
                }
            }
        }
    }
}
