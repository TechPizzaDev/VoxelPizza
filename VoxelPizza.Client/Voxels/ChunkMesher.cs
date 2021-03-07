using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelPizza.Client
{
    public static class SpanCastExtensions
    {
        public static Span<TTo> Cast<T, TTo>(this Span<T> from)
            where T : struct
            where TTo : struct
        {
            return MemoryMarshal.Cast<T, TTo>(from);
        }

        public static ReadOnlySpan<TTo> Cast<T, TTo>(this ReadOnlySpan<T> from)
            where T : struct
            where TTo : struct
        {
            return MemoryMarshal.Cast<T, TTo>(from);
        }
    }

    public class ChunkMesher
    {
        public ArrayPool<byte> ArrayPool { get; }

        public ChunkMesher(ArrayPool<byte> arrayPool)
        {
            ArrayPool = arrayPool ?? throw new ArgumentNullException(nameof(arrayPool));
        }

        public ChunkMeshResult Mesh(Chunk chunk, Span<byte> initialBuffer = default)
        {
            int elementByteSum = 
                sizeof(uint) * 6 
                + Unsafe.SizeOf<ChunkSpaceVertex>() * 4
                + Unsafe.SizeOf<ChunkPaintVertex>() * 4;
            int initialIndexBufferLength = initialBuffer.Length / elementByteSum * 6;
            int initialVertexBufferLength = initialBuffer.Length / elementByteSum * 4;

            int bufferOffset = 0;

            Span<byte> initialIndexBuffer = initialBuffer.Slice(
                bufferOffset, initialIndexBufferLength * sizeof(uint));
            bufferOffset += initialIndexBuffer.Length;

            Span<byte> initialSpaceVertexBuffer = initialBuffer.Slice(
                bufferOffset, initialVertexBufferLength * Unsafe.SizeOf<ChunkSpaceVertex>());
            bufferOffset += initialSpaceVertexBuffer.Length;

            Span<byte> initialPaintVertexBuffer = initialBuffer.Slice(
                bufferOffset, initialVertexBufferLength * Unsafe.SizeOf<ChunkPaintVertex>());

            var ind = new ByteStore<uint>(ArrayPool, null, initialIndexBuffer);
            var spa = new ByteStore<ChunkSpaceVertex>(ArrayPool, null, initialSpaceVertexBuffer);
            var pai = new ByteStore<ChunkPaintVertex>(ArrayPool, null, initialPaintVertexBuffer);

            var indGen = new CubeIndexGenerator();
            var indPro = new CubeIndexProvider<CubeIndexGenerator, uint>(indGen, CubeFaces.All);
            uint vertexOffset = 0;

            TextureAnimation[] anims = new TextureAnimation[]
            {
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1f),
                TextureAnimation.Create(TextureAnimationType.Step, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 3, 1.5f),
                TextureAnimation.Create(TextureAnimationType.Step, 2, 1.5f),
                TextureAnimation.Create(TextureAnimationType.MixStep, 2, 1.5f),
            };

            uint[,,] blocks = chunk.Blocks;

            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        uint id = blocks[y, z, x];
                        if (id == 0)
                            continue;

                        var spaGen = new CubeSpaceVertexGenerator(new Vector3(x, y, z));

                        var anim = anims[id - 1];
                        var paiGen = new CubePaintVertexGenerator(anim, 0);

                        var spaPro = new CubeVertexProvider<CubeSpaceVertexGenerator, ChunkSpaceVertex>(spaGen, CubeFaces.All);
                        var paiPro = new CubeVertexProvider<CubePaintVertexGenerator, ChunkPaintVertex>(paiGen, spaPro.Faces);

                        spaPro.AppendVertices(ref spa);
                        paiPro.AppendVertices(ref pai);
                        indPro.AppendIndices(ref ind, ref vertexOffset);
                    }
                }
            }

            return new ChunkMeshResult(ind, spa, pai);
        }
    }
}