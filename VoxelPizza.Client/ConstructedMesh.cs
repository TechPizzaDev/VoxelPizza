using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public abstract class ConstructedMesh
    {
        /// <summary>
        /// The vertices of the mesh.
        /// </summary>
        public VertexPositionNormalTexture[] Vertices { get; }

        /// <summary>
        /// The name of the <see cref="MaterialDefinition"/> associated with this mesh.
        /// </summary>
        public string? MaterialName { get; }

        public abstract int IndexCount { get; }

        public abstract IndexFormat IndexFormat { get; }

        public ConstructedMesh(VertexPositionNormalTexture[] vertices, string? materialName)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            MaterialName = materialName;
        }

        public DeviceBuffer CreateVertexBuffer(ResourceFactory factory, CommandList cl)
        {
            DeviceBuffer vb = factory.CreateBuffer(
                new BufferDescription(Vertices.SizeInBytes(), BufferUsage.VertexBuffer));
            cl.UpdateBuffer(vb, 0, Vertices);
            return vb;
        }

        public abstract DeviceBuffer CreateIndexBuffer(ResourceFactory factory, CommandList cl);

        public unsafe BoundingSphere GetBoundingSphere()
        {
            fixed (VertexPositionNormalTexture* ptr = Vertices)
            {
                return BoundingSphere.CreateFromPoints((Vector3*)ptr, Vertices.Length, Unsafe.SizeOf<VertexPositionNormalTexture>());
            }
        }

        public unsafe BoundingBox GetBoundingBox()
        {
            fixed (VertexPositionNormalTexture* ptr = Vertices)
            {
                return BoundingBox.CreateFromPoints(
                    (Vector3*)ptr,
                    Vertices.Length,
                    Unsafe.SizeOf<VertexPositionNormalTexture>(),
                    Quaternion.Identity,
                    Vector3.Zero,
                    Vector3.One);
            }
        }

        public void GetVertexPositions(Span<Vector3> destination)
        {
            ReadOnlySpan<VertexPositionNormalTexture> src = Vertices.AsSpan(0, destination.Length);
            for (int i = 0; i < src.Length; i++)
                destination[i] = src[i].Position;
        }

        public Vector3[] GetVertexPositions()
        {
            var array = new Vector3[Vertices.Length];
            GetVertexPositions(array);
            return array;
        }
    }
}
