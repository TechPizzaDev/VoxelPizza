using System;
using VoxelPizza.Collections;
using VoxelPizza.Memory;
using VoxelPizza.Numerics;

namespace VoxelPizza.World;

public class PlaneTerrainGenerator : TerrainGenerator
{
    public int LevelY;

    public override bool CanGenerate(ChunkPosition position)
    {
        return position.Y >= LevelY - 1 && position.Y <= LevelY + 1;
    }

    public override ChunkTicket CreateTicket(ValueArc<Chunk> chunk)
    {
        return new PlaneTerrainTicket(chunk.Wrap(), this);
    }

    public class PlaneTerrainTicket : ChunkTicket
    {
        public PlaneTerrainGenerator Generator { get; }

        public PlaneTerrainTicket(ValueArc<Chunk> chunk, PlaneTerrainGenerator generator) : base(chunk.Wrap())
        {
            Generator = generator;
        }

        public override GeneratorState Work(GeneratorState state)
        {
            if (state != GeneratorState.Complete)
            {
                return TransitionState(state);
            }

            Chunk chunk = GetChunk().Get();
            ChunkPosition chunkPos = chunk.Position;

            if (chunkPos.Y != Generator.LevelY)
            {
                return TransitionState(GeneratorState.Complete);
            }

            BlockStorage blockStorage = chunk.GetBlockStorage();

            ulong seed = 17;
            seed = seed * 31 + (uint)chunkPos.X;
            seed = seed * 31 + (uint)chunkPos.Y;
            seed = seed * 31 + (uint)chunkPos.Z;
            XoshiroRandom rng = new(seed);

            Span<byte> tmp8 = stackalloc byte[Chunk.Width];
            Span<uint> tmp32 = stackalloc uint[Chunk.Width];

            for (int z = 0; z < Chunk.Depth; z++)
            {
                rng.NextBytes(tmp8);
                BlockStorage.Expand8To32(tmp8, tmp32);
                blockStorage.SetBlockRow(0, 0, z, tmp32);
            }

            return TransitionState(GeneratorState.Complete);
        }
    }
}