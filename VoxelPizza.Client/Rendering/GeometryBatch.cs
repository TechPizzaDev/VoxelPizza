using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Veldrid;

namespace VoxelPizza.Client
{
    public class GeometryBatch<TVertex> : GraphicsResource
        where TVertex : unmanaged
    {
        private List<DeviceBuffer> _indexBuffers = new();
        private List<DeviceBuffer> _vertexBuffers = new();
        private List<uint> _indexCounts = new();
        private int _bufferCount;

        private GraphicsDevice _device;
        private bool _begun;
        private MappedResourceView<uint> _indexView;
        private MappedResourceView<TVertex> _vertexView;
        private uint _indexOffset;
        private uint _vertexOffset;

        public uint IndicesPerBuffer { get; }
        public uint VerticesPerBuffer { get; }

        public GeometryBatch(uint indicesPerBuffer, uint verticesPerBuffer)
        {
            if (indicesPerBuffer < 1 || indicesPerBuffer * sizeof(uint) > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(indicesPerBuffer));
            if (verticesPerBuffer < 1 || verticesPerBuffer * Unsafe.SizeOf<TVertex>() > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(verticesPerBuffer));

            IndicesPerBuffer = indicesPerBuffer;
            VerticesPerBuffer = verticesPerBuffer;
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _device = gd ?? throw new ArgumentNullException(nameof(gd));
        }

        public override void DestroyDeviceObjects()
        {
            if (_begun)
                End();

            foreach (DeviceBuffer buffer in _indexBuffers)
                buffer.Dispose();
            _indexBuffers.Clear();

            foreach (DeviceBuffer buffer in _vertexBuffers)
                buffer.Dispose();
            _vertexBuffers.Clear();

            _bufferCount = 0;
            _device = null!;
        }

        private void FinishCurrentBuffers()
        {
            if (_indexOffset > 0)
            {
                _indexCounts.Add(_indexOffset);

                _device.Unmap(_indexView.MappedResource.Resource);
                _indexView = default;

                _device.Unmap(_vertexView.MappedResource.Resource);
                _vertexView = default;

                _indexOffset = 0;
                _vertexOffset = 0;
            }
        }

        private void PushBuffers()
        {
            if (_device == null || !_begun)
                throw new InvalidOperationException();

            FinishCurrentBuffers();

            ResourceFactory factory = _device.ResourceFactory;

            DeviceBuffer indexBuffer;
            DeviceBuffer vertexBuffer;
            if (_bufferCount >= _indexBuffers.Count)
            {
                indexBuffer = factory.CreateBuffer(new BufferDescription(
                    IndicesPerBuffer * sizeof(uint),
                    BufferUsage.IndexBuffer | BufferUsage.DynamicWrite));
                _indexBuffers.Add(indexBuffer);

                vertexBuffer = factory.CreateBuffer(new BufferDescription(
                    VerticesPerBuffer * (uint)Unsafe.SizeOf<TVertex>(),
                    BufferUsage.VertexBuffer | BufferUsage.DynamicWrite));
                _vertexBuffers.Add(vertexBuffer);
            }
            else
            {
                indexBuffer = _indexBuffers[_bufferCount];
                vertexBuffer = _vertexBuffers[_bufferCount];
            }

            _indexView = _device.Map<uint>(indexBuffer, MapMode.Write);
            _vertexView = _device.Map<TVertex>(vertexBuffer, MapMode.Write);
            _bufferCount++;
        }

        public void Begin()
        {
            if (_device == null || _begun)
                throw new InvalidOperationException();

            _begun = true;
            _bufferCount = 0;
            _indexCounts.Clear();

            PushBuffers();
        }

        public void Clear()
        {
            Begin();
            End();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureExtraCapacity(uint extraLength, string? paramName)
        {
            if (extraLength + _indexOffset > IndicesPerBuffer)
            {
                if (!_begun)
                    throw new InvalidOperationException();

                if (extraLength > IndicesPerBuffer)
                {
                    throw new ArgumentException(
                        "The given amount of indices exceeds buffer capacity.", paramName);
                }
                PushBuffers();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendUnsafe(ReadOnlySpan<uint> indices, ReadOnlySpan<TVertex> vertices)
        {
            indices.CopyTo(_indexView.AsSpan(_indexOffset));
            _indexOffset += (uint)indices.Length;

            vertices.CopyTo(_vertexView.AsSpan(_vertexOffset));
            _vertexOffset += (uint)vertices.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(ReadOnlySpan<uint> indices, ReadOnlySpan<TVertex> vertices)
        {
            EnsureExtraCapacity((uint)indices.Length, nameof(indices));
            AppendUnsafe(indices, vertices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AppendQuad(in TVertex v0, in TVertex v1, in TVertex v2, in TVertex v3)
        {
            var (vertexOffset, indices, vertices) = ReserveUnsafe(6, 4);

            indices[0] = vertexOffset + 0;
            indices[1] = vertexOffset + 1;
            indices[2] = vertexOffset + 2;
            indices[3] = vertexOffset + 0;
            indices[4] = vertexOffset + 2;
            indices[5] = vertexOffset + 3;

            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;
            vertices[3] = v3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UnsafeReserve ReserveUnsafe(int indexCount, int vertexCount)
        {
            EnsureExtraCapacity((uint)indexCount, nameof(indexCount));

            uint vertexOffset = _vertexOffset;

            uint* indices = (uint*)_indexView.MappedResource.Data + _indexOffset;
            TVertex* vertices = (TVertex*)_vertexView.MappedResource.Data + vertexOffset;

            _indexOffset += (uint)indexCount;
            _vertexOffset += (uint)vertexCount;

            return new UnsafeReserve(vertexOffset, indices, vertices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UnsafeReserve ReserveQuadsUnsafe(int quadCount)
        {
            UnsafeReserve reserve = ReserveUnsafe(quadCount * 6, quadCount * 4);
            for (uint i = 0, v = reserve.VertexOffset; i < quadCount; i++, v += 4)
            {
                uint* indexPtr = reserve.Indices + i * 6;
                indexPtr[0] = v + 0;
                indexPtr[1] = v + 1;
                indexPtr[2] = v + 2;
                indexPtr[3] = v + 0;
                indexPtr[4] = v + 2;
                indexPtr[5] = v + 3;
            }
            return reserve;
        }

        public void End()
        {
            if (_device == null || !_begun)
                throw new InvalidOperationException();

            _begun = false;

            if (_indexOffset == 0)
                _bufferCount--;

            FinishCurrentBuffers();
        }

        public void Submit(CommandList commandList)
        {
            for (int i = 0; i < _bufferCount; i++)
            {
                DeviceBuffer indexBuffer = _indexBuffers[i];
                DeviceBuffer vertexBuffer = _vertexBuffers[i];
                uint indexCount = _indexCounts[i];

                commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt32);
                commandList.SetVertexBuffer(0, vertexBuffer);
                commandList.DrawIndexed(indexCount);
            }
        }

        public readonly unsafe struct UnsafeReserve
        {
            public readonly uint VertexOffset;
            public readonly uint* Indices;
            public readonly TVertex* Vertices;

            public UnsafeReserve(uint vertexOffset, uint* indices, TVertex* vertices)
            {
                VertexOffset = vertexOffset;
                Indices = indices;
                Vertices = vertices;
            }

            public void Deconstruct(out uint vertexOffset, out uint* indices, out TVertex* vertices)
            {
                vertexOffset = VertexOffset;
                indices = Indices;
                vertices = Vertices;
            }
        }
    }
}
