using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client.Objects
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ParticleInstance
    {
        public Vector4 Position;
        public Vector4 Velocity;
        public Vector4 Color;
    }

    internal class ParticlePlane : Renderable
    {
        private static ushort[] s_quadIndices = new ushort[] { 0, 1, 2, 0, 2, 3 };
        private static float[] s_quadVertices = new float[] {
            0, 0, 0, 0,
            1, 0, 1, 0,
            1, 1, 1, 1,
            0, 1, 0, 1
        };

        private DisposeCollector _disposeCollector;
        private Pipeline _pipeline;
        private ResourceSet _sharedResourceSet;
        private DeviceBuffer _cameraProjViewBuffer;
        private DeviceBuffer _ib;
        private DeviceBuffer _vb;
        private DeviceBuffer _instanceVb;

        public Camera Camera { get; }

        Random rng = new Random(1234);
        float range = 500;

        ParticleInstance[] particles;

        public ParticlePlane(Camera camera)
        {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));


            particles = new ParticleInstance[1_000_000];
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Position = GetRandomVec4() * range - new Vector4(range / 2f);
                particles[i].Position.W = 0;

                particles[i].Color = new Vector4(i / (float)particles.Length, 0, 0, 0);
            }
        }

        public Vector4 GetRandomVec4()
        {
            return new Vector4(
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                (float)rng.NextDouble(),
                1);
        }

        float waveRange = 10;

        public void Update(in FrameTime time)
        {
            float delta = time.DeltaSeconds;

            Vector4 acceleration = new Vector4(0, -100f, 0, 0);

            float halfRange = range / 2f;
            BoundingBox box = new BoundingBox(
                new Vector3(-halfRange, 0, -halfRange),
                new Vector3(halfRange, 0, halfRange));

            Ray ray = new Ray(Camera.Position, Camera.LookDirection);
            bool intersect = ray.Intersects(box, out float distance);
            Vector3 raypoint = ray.GetPoint(distance);
            Vector4 raypoint4 = new(raypoint, 1);

            ImGuiNET.ImGui.Begin("Particle Plane");

            ImGuiNET.ImGui.Text(intersect.ToString());
            ImGuiNET.ImGui.Text(distance.ToString());

            ImGuiNET.ImGui.SliderFloat("Wave Range", ref waveRange, 0, 1000);

            ImGuiNET.ImGui.End();

            for (int i = 0; i < particles.Length; i++)
            {
                ref ParticleInstance particle = ref particles[i];

                ref Vector4 position = ref particle.Position;

                if (intersect)
                {
                    float distanceToRayPoint = Vector4.DistanceSquared(position, raypoint4);
                    if (distanceToRayPoint < waveRange * waveRange)
                    {
                        particle.Velocity += new Vector4(0, (500 - acceleration.Y) * delta, 0, 0);
                    }
                }

                particle.Velocity += acceleration * delta;

                position += particle.Velocity * delta;

                if (position.Y < 0f)
                {
                    position.Y = 0f;
                    particle.Velocity = Vector4.Zero;
                }

                //position = Vector4.Lerp(position, target, delta);
                //
                //float distSqr = Vector4.DistanceSquared(position, target);
                //if (distSqr < 1f)
                //{
                //    position = GetRandomVec4() * range - new Vector4(range / 2f);
                //}
            }
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            DisposeCollectorResourceFactory factory = new(gd.ResourceFactory);
            _disposeCollector = factory.DisposeCollector;

            (Shader vs, Shader fs) = StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ParticlePlane");

            VertexLayoutDescription sharedVertexLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));

            VertexLayoutDescription vertexLayoutPerInstance = new VertexLayoutDescription(
                new VertexElementDescription("InstancePosition", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
                new VertexElementDescription("InstanceVelocity", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
                new VertexElementDescription("InstanceColor", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));
            vertexLayoutPerInstance.InstanceStepRate = 1;

            ResourceLayoutElementDescription[] resourceLayoutElementDescriptions =
            {
                new ResourceLayoutElementDescription("ProjView", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            };
            ResourceLayoutDescription resourceLayoutDescription = new ResourceLayoutDescription(resourceLayoutElementDescriptions);
            ResourceLayout sharedLayout = factory.CreateResourceLayout(resourceLayoutDescription);

            GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                new BlendStateDescription(
                    RgbaFloat.Black,
                    BlendAttachmentDescription.OverrideBlend,
                    BlendAttachmentDescription.OverrideBlend),
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[]
                    {
                        sharedVertexLayout,
                        vertexLayoutPerInstance
                    },
                    new[] { vs, fs, },
                    ShaderHelper.GetSpecializations(gd)),
                new ResourceLayout[] { sharedLayout },
                sc.MainSceneFramebuffer.OutputDescription);
            _pipeline = factory.CreateGraphicsPipeline(ref pd);

            _cameraProjViewBuffer = factory.CreateBuffer(
                new BufferDescription((uint)(Unsafe.SizeOf<Matrix4x4>() * 2), BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            _vb = factory.CreateBuffer(
                new BufferDescription((uint)s_quadVertices.Length * sizeof(float), BufferUsage.VertexBuffer));
            cl.UpdateBuffer(_vb, 0, s_quadVertices);

            _ib = factory.CreateBuffer(
                new BufferDescription((uint)s_quadIndices.Length * sizeof(ushort), BufferUsage.IndexBuffer));
            cl.UpdateBuffer(_ib, 0, s_quadIndices);

            _instanceVb = factory.CreateBuffer(new BufferDescription(particles.SizeInBytes(), BufferUsage.VertexBuffer | BufferUsage.Dynamic));

            ResourceSetDescription resourceSetDescription = new ResourceSetDescription(sharedLayout, new[] { _cameraProjViewBuffer });
            _sharedResourceSet = factory.CreateResourceSet(resourceSetDescription);
        }

        public override void DestroyDeviceObjects()
        {
            _disposeCollector.DisposeAll();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey();
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            cl.UpdateBuffer(_cameraProjViewBuffer, 0, new MatrixPair(Camera.ViewMatrix, Camera.ProjectionMatrix));
            cl.UpdateBuffer(_instanceVb, 0, particles);

            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _sharedResourceSet);
            cl.SetVertexBuffer(0, _vb);
            cl.SetVertexBuffer(1, _instanceVb);
            cl.SetIndexBuffer(_ib, IndexFormat.UInt16);
            cl.DrawIndexed(6, (uint)particles.Length, 0, 0, 0);
        }

        public override RenderPasses RenderPasses => RenderPasses.Standard;

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }

        public struct MatrixPair
        {
            public Matrix4x4 First;
            public Matrix4x4 Second;

            public MatrixPair(Matrix4x4 first, Matrix4x4 second)
            {
                First = first;
                Second = second;
            }
        }
    }
}
