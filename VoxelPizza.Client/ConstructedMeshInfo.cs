using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    /// <summary>
    /// A standalone <see cref="MeshData"/> created from information from an <see cref="ObjFile"/>.
    /// </summary>
    public class ConstructedMeshInfo : MeshData
    {
        /// <summary>
        /// The vertices of the mesh.
        /// </summary>
        public VertexPositionNormalTexture[] Vertices { get; }

        /// <summary>
        /// The indices of the mesh.
        /// </summary>
        public ushort[] Indices { get; }

        /// <summary>
        /// The name of the <see cref="MaterialDefinition"/> associated with this mesh.
        /// </summary>
        public string? MaterialName { get; }

        /// <summary>
        /// Constructs a new <see cref="ConstructedMeshInfo"/>.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="indices">The indices.</param>
        /// <param name="materialName">The name of the associated MTL <see cref="MaterialDefinition"/>.</param>
        public ConstructedMeshInfo(VertexPositionNormalTexture[] vertices, ushort[] indices, string? materialName)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
            MaterialName = materialName;
        }

        public DeviceBuffer CreateVertexBuffer(ResourceFactory factory, CommandList cl)
        {
            DeviceBuffer vb = factory.CreateBuffer(
                new BufferDescription((uint)(Vertices.Length * Unsafe.SizeOf<VertexPositionNormalTexture>()), BufferUsage.VertexBuffer));
            cl.UpdateBuffer(vb, 0, Vertices);
            return vb;
        }

        public DeviceBuffer CreateIndexBuffer(ResourceFactory factory, CommandList cl, out int indexCount)
        {
            DeviceBuffer ib = factory.CreateBuffer(new BufferDescription((uint)(Indices.Length * sizeof(int)), BufferUsage.IndexBuffer));
            cl.UpdateBuffer(ib, 0, Indices);
            indexCount = Indices.Length;
            return ib;
        }

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

        public bool RayCast(Ray ray, out float distance)
        {
            distance = float.MaxValue;
            bool result = false;
            for (int i = 0; i < Indices.Length - 2; i += 3)
            {
                Vector3 v0 = Vertices[Indices[i + 0]].Position;
                Vector3 v1 = Vertices[Indices[i + 1]].Position;
                Vector3 v2 = Vertices[Indices[i + 2]].Position;

                if (ray.Intersects(ref v0, ref v1, ref v2, out float newDistance))
                {
                    if (newDistance < distance)
                    {
                        distance = newDistance;
                    }

                    result = true;
                }
            }

            return result;
        }

        public int RayCast(Ray ray, List<float> distances)
        {
            int hits = 0;
            for (int i = 0; i < Indices.Length - 2; i += 3)
            {
                Vector3 v0 = Vertices[Indices[i + 0]].Position;
                Vector3 v1 = Vertices[Indices[i + 1]].Position;
                Vector3 v2 = Vertices[Indices[i + 2]].Position;

                if (ray.Intersects(ref v0, ref v1, ref v2, out float newDistance))
                {
                    hits++;
                    distances.Add(newDistance);
                }
            }

            return hits;
        }

        public Vector3[] GetVertexPositions()
        {
            Vector3[] array = new Vector3[Vertices.Length];
            Span<VertexPositionNormalTexture> src = Vertices.AsSpan();
            Span<Vector3> dst = array.AsSpan(0, src.Length);
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i].Position;
            return array;
        }

        public ushort[] GetIndices()
        {
            return Indices;
        }
    }
}
