using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelPizza.Client
{
    public class ChunkMesher
    {
        public HeapPool Pool { get; }

        public ChunkMesher(HeapPool pool)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        [SkipLocalsInit]
        public unsafe ChunkMeshResult Mesh(
            Chunk chunk,
            Chunk? frontChunk,
            Chunk? backChunk,
            Chunk? topChunk,
            Chunk? bottomChunk,
            Chunk? leftChunk,
            Chunk? rightChunk)
        {
            var ind = new ByteStore<uint>(Pool);
            var spa = new ByteStore<ChunkSpaceVertex>(Pool);
            var pai = new ByteStore<ChunkPaintVertex>(Pool);

            uint vertexOffset = 0;

            BlockDescription[] descs = new BlockDescription[]
            {
                new BlockDescription(CubeFaces.None),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
                new BlockDescription(CubeFaces.All),
            };

            TextureAnimation[] anims = new TextureAnimation[]
            {
                default,
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1f),
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
            };
            anims = new TextureAnimation[]
            {
                default,
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
                TextureAnimation.Create(TextureAnimationType.Step, 1, 0f),
            };

            uint* centerRow = stackalloc uint[Chunk.Width + 2];
            uint* bottomRow = stackalloc uint[Chunk.Width];
            uint* topRow = stackalloc uint[Chunk.Width];
            uint* frontRow = stackalloc uint[Chunk.Width];
            uint* backRow = stackalloc uint[Chunk.Width];
            Span<uint> centerSpan = new(centerRow + 1, Chunk.Width);
            Span<uint> bottomSpan = new(bottomRow, Chunk.Width);
            Span<uint> topSpan = new(topRow, Chunk.Width);
            Span<uint> frontSpan = new(frontRow, Chunk.Width);
            Span<uint> backSpan = new(backRow, Chunk.Width);

            void GetBlockRow(int y, int z, Span<uint> destination)
            {
                if (y == -1)
                {
                    if (bottomChunk == null)
                        goto Clear;
                    bottomChunk.GetBlockRow(Chunk.Height - 1, z, destination);
                }
                else if (y == Chunk.Height)
                {
                    if (topChunk == null)
                        goto Clear;
                    topChunk.GetBlockRow(0, z, destination);
                }
                else if (z == -1)
                {
                    if (backChunk == null)
                        goto Clear;
                    backChunk.GetBlockRow(y, Chunk.Depth - 1, destination);
                }
                else if (z == Chunk.Depth)
                {
                    if (frontChunk == null)
                        goto Clear;
                    frontChunk.GetBlockRow(y, 0, destination);
                }
                else
                {
                    chunk.GetBlockRow(y, z, destination);
                }

                return;

                Clear:
                destination.Clear();
            }

            for (int y = 0; y < Chunk.Height; y++)
            {
                GetBlockRow(y, -1, backSpan);
                GetBlockRow(y, 0, centerSpan);
                centerRow[0] = 0;
                centerRow[Chunk.Width + 1] = 0;

                for (int z = 0; z < Chunk.Depth; z++)
                {
                    if (leftChunk != null)
                        centerRow[0] = leftChunk.GetBlock(Chunk.Width - 1, y, z);
                    if (rightChunk != null)
                        centerRow[Chunk.Width + 1] = rightChunk.GetBlock(0, y, z);

                    GetBlockRow(y - 1, z, bottomSpan);
                    GetBlockRow(y + 1, z, topSpan);
                    GetBlockRow(y, z + 1, frontSpan);

                    AppendBody(
                        ref ind, ref spa, ref pai, ref vertexOffset, y, z,
                        descs, anims,
                        centerRow + 1, bottomRow, topRow, frontRow, backRow);

                    centerSpan.CopyTo(backSpan);
                    frontSpan.CopyTo(centerSpan);
                }
            }
            return new ChunkMeshResult(ind, spa, pai);
        }

        private unsafe void AppendBody(
            ref ByteStore<uint> ind,
            ref ByteStore<ChunkSpaceVertex> spa,
            ref ByteStore<ChunkPaintVertex> pai,
            ref uint vertexOffset,
            float y,
            float z,
            BlockDescription[] descs,
            TextureAnimation[] anims,
            uint* centerRow,
            uint* bottomRow,
            uint* topRow,
            uint* frontRow,
            uint* backRow)
        {
            for (int x = 0; x < Chunk.Width; x++)
            {
                uint id = centerRow[x];
                if (id == 0)
                    continue;

                ref TextureAnimation anim = ref anims[id];

                CubeFaces faces = CubeFaces.All;

                if ((descs[centerRow[x - 1]].BlockingFaces & CubeFaces.Right) != 0)
                {
                    faces &= ~CubeFaces.Left;
                }

                if ((descs[centerRow[x + 1]].BlockingFaces & CubeFaces.Left) != 0)
                {
                    faces &= ~CubeFaces.Right;
                }

                if ((descs[topRow[x]].BlockingFaces & CubeFaces.Bottom) != 0)
                {
                    faces &= ~CubeFaces.Top;
                }

                if ((descs[bottomRow[x]].BlockingFaces & CubeFaces.Top) != 0)
                {
                    faces &= ~CubeFaces.Bottom;
                }

                if ((descs[frontRow[x]].BlockingFaces & CubeFaces.Back) != 0)
                {
                    faces &= ~CubeFaces.Front;
                }

                if ((descs[backRow[x]].BlockingFaces & CubeFaces.Front) != 0)
                {
                    faces &= ~CubeFaces.Back;
                }

                var spaGen = new CubeSpaceVertexGenerator(new Vector3(x, y, z));

                var paiGen = new CubePaintVertexGenerator(anim, id);

                var spaPro = new CubeVertexProvider<CubeSpaceVertexGenerator, ChunkSpaceVertex>(spaGen, faces);
                var paiPro = new CubeVertexProvider<CubePaintVertexGenerator, ChunkPaintVertex>(paiGen, faces);

                var indGen = new CubeIndexGenerator();
                var indPro = new CubeIndexProvider<CubeIndexGenerator, uint>(indGen, faces);

                spaPro.AppendVertices(ref spa);
                paiPro.AppendVertices(ref pai);
                indPro.AppendIndices(ref ind, ref vertexOffset);
            }
        }
    }

    public readonly struct BlockDescription
    {
        public CubeFaces BlockingFaces { get; }

        public BlockDescription(CubeFaces blockingFaces)
        {
            BlockingFaces = blockingFaces;
        }
    }

    public struct BlockPosition
    {
        public int X;
        public int Y;
        public int Z;

        public BlockPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int x, int y, int z)
        {
            return x + Chunk.Width * (y + Chunk.Depth * z);
        }
    }
}