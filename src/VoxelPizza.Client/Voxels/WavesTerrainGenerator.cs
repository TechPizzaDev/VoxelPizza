using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpFastNoise2;
using SharpFastNoise2.Functions;
using VoxelPizza.Collections.Blocks;
using VoxelPizza.Memory;

namespace VoxelPizza.World;

public class WavesTerrainGenerator : TerrainGenerator
{
    public override bool CanGenerate(ChunkPosition position)
    {
        if (position.Y < -1)
        {
            return false;
        }
        if (position.Y > 6)
        {
            return false;
        }
        return true;
    }

    public override ChunkTicket CreateTicket(ValueArc<Chunk> chunk)
    {
        return new WavesTerrainTicket(chunk.Wrap());
    }

    public class WavesTerrainTicket : ChunkTicket
    {
        public WavesTerrainTicket(ValueArc<Chunk> chunk) : base(chunk.Wrap())
        {
        }

        public override GeneratorState Work(GeneratorState state)
        {
            if (state != GeneratorState.Complete)
            {
                return TransitionState(state);
            }

            if (IsStopRequested)
            {
                return State;
            }

            Chunk chunk = GetChunk().Get();
            ChunkPosition chunkPos = chunk.Position;

            BlockPosition blockOrigin = chunkPos.ToBlock();
            BlockStorage blockStorage = chunk.GetBlockStorage();

            Span<int> layerBuffer = stackalloc int[Chunk.Width * Chunk.Depth];

            for (int y = 0; y < Chunk.Height; y++)
            {
                if (IsStopRequested)
                {
                    return State;
                }

                if (Avx2Functions.IsSupported)
                {
                    GenLayerBody<Vector256<uint>, Vector256<float>, Vector256<int>, Avx2Functions> body = new(blockOrigin, y);
                    RunBody(body, layerBuffer);
                }
                else
                {
                    GenLayerBody<Vector128<uint>, Vector128<float>, Vector128<int>, Sse2Functions> body = new(blockOrigin, y);
                    RunBody(body, layerBuffer);
                }

                blockStorage.SetBlockLayer(y, MemoryMarshal.Cast<int, uint>(layerBuffer));
            }

            return TransitionState(GeneratorState.Complete);
        }

        private struct GenLayerBody<m32, f32, i32, F>(BlockPosition origin, int y) : IGenBody<int>
            where m32 : unmanaged
            where f32 : unmanaged
            where i32 : unmanaged
            where F : IFunctionList<m32, f32, i32, F>
        {
            public static int Length => Chunk.Depth;
            public static int Stride => Chunk.Width;
            public static int StepSize => F.Count;

            public BlockPosition origin = origin;
            public f32 vBlockY = F.Broad((float)(origin.Y + y));
            public f32 vBlockZ = F.Add(F.Broad(origin.Z / 16f), F.Mul(F.Incremented_f32(), F.Broad(1f / 16f)));

            public readonly void Run(Span<int> rows) => Run(F.Count, rows);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Run(int count, Span<int> rows)
            {
                Debug.Assert((uint)count <= (uint)F.Count);

                f32 cos = F.Mul(F.Add(Utils<m32, f32, i32, F>.Cos_f32(vBlockZ), F.Broad(1f)), F.Broad(32f));

                m32 cond = F.GreaterThanOrEqual(F.Mul(cos, F.Broad(0.5f)), vBlockY);
                if (count == F.Count && F.AllMask(cond))
                {
                    rows.Fill(1);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        Span<int> row = rows.Slice(i * Chunk.Width, Chunk.Width);

                        f32 rowCos = F.Broad(F.Extract(cos, i));
                        GenRowBody<m32, f32, i32, F> rowBody = new(rowCos, vBlockY, origin.X);
                        RunBody(rowBody, row);
                    }
                }
            }

            public void Step() => Step(F.Count);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Step(int count)
            {
                vBlockZ = F.Add(vBlockZ, F.Broad(count / 16f));
            }
        }

        private struct GenRowBody<m32, f32, i32, F>(f32 cos, f32 blockY, float blockX) : IGenBody<int>
            where m32 : unmanaged
            where f32 : unmanaged
            where i32 : unmanaged
            where F : IFunctionList<m32, f32, i32, F>
        {
            public static int Length => Chunk.Width;
            public static int Stride => 1;
            public static int StepSize => F.Count;

            public f32 vBlockX = F.Add(F.Broad(blockX / 16f), F.Mul(F.Incremented_f32(), F.Broad(1f / 16f)));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly i32 Run()
            {
                f32 vSin = Utils<m32, f32, i32, F>.Sin_f32(vBlockX);
                f32 vScaledSin = F.Mul(F.Add(vSin, F.Broad((float)1)), F.Broad(32f));

                m32 cond = F.GreaterThanOrEqual(F.Mul(F.Add(vScaledSin, cos), F.Broad(0.5f)), blockY);
                i32 v = F.Select(cond, F.Broad(1), F.Broad(0));
                return v;
            }

            public readonly void Run(Span<int> row)
            {
                i32 v = Run();
                F.Store(row, v);
            }

            public readonly void Run(int count, Span<int> row)
            {
                i32 v = Run();
                for (int i = 0; i < count; i++)
                {
                    row[i] = F.Extract(v, i);
                }
            }

            public void Step() => Step(F.Count);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Step(int count)
            {
                vBlockX = F.Add(vBlockX, F.Broad(count / 16f));
            }
        }

        private static void RunBody<T, E>(T body, Span<E> destination)
            where T : IGenBody<E>
        {
            int i = 0;
            for (; i + T.StepSize <= T.Length; i += T.StepSize)
            {
                Span<E> span = destination.Slice(i * T.Stride, T.Stride * T.StepSize);
                body.Run(span);
                body.Step();
            }

            int tailCount = T.Length - i;
            if (tailCount > 0)
            {
                Span<E> tailSpan = destination.Slice(i * T.Stride, T.Stride * tailCount);
                body.Run(tailCount, tailSpan);
            }
        }

        private interface IGenBody<T>
        {
            static abstract int Length { get; }
            static abstract int Stride { get; }
            static abstract int StepSize { get; }

            void Run(Span<T> destination);
            void Run(int count, Span<T> destination);

            void Step();
            void Step(int count);
        }
    }
}
