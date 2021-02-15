using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Veldrid;
using Veldrid.Utilities;
using VoxelPizza.Numerics;

namespace VoxelPizza.Client
{
    public class ChunkRenderer : GraphicsResource, IUpdateable
    {
        private static ushort[] s_quadIndices = new ushort[] { 0, 1, 2, 0, 2, 3 };

        private DisposeCollector _disposeCollector;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;
        private Pipeline _pipeline;
        private ResourceSet _sharedSet;

        private List<ChunkVisual> _visibleChunkBuffer = new();

        private List<ChunkVisual> _visuals = new List<ChunkVisual>();

        private WorldInfo _worldInfo;

        public Camera Camera { get; }

        public ResourceLayout ChunkInfoLayout { get; private set; }

        public ChunkRenderer(Camera camera)
        {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));

            _visuals.Add(new ChunkVisual(this));
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            DisposeCollectorResourceFactory factory = new(gd.ResourceFactory);
            _disposeCollector = factory.DisposeCollector;

            (Shader vs, Shader fs) = StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkMain");

            ResourceLayout sharedLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ProjectionMatrix", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("ViewMatrix", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("LightInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("WorldInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("TextureAtlas", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment)));

            ChunkInfoLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ChunkInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            VertexLayoutDescription spaceLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Half4),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription paintLayout = new VertexLayoutDescription(
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Byte4),
                new VertexElementDescription("TexAnimation0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
                new VertexElementDescription("TexRegion0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { spaceLayout, paintLayout },
                    new[] { vs, fs, },
                    ShaderHelper.GetSpecializations(gd)),
                new[] { sharedLayout, ChunkInfoLayout },
                sc.MainSceneFramebuffer.OutputDescription));

            _worldInfoBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<WorldInfo>(), BufferUsage.UniformBuffer));

            TextureRegion[] regions = new TextureRegion[]
            {
                new TextureRegion(0, 0, 0, 127, 0, 0)
            };
            _textureAtlasBuffer = factory.CreateBuffer(new BufferDescription(
                regions.SizeInBytes(), BufferUsage.StructuredBufferReadOnly, (uint)Unsafe.SizeOf<TextureRegion>()));
            gd.UpdateBuffer(_textureAtlasBuffer, 0, regions);

            _sharedSet = factory.CreateResourceSet(new ResourceSetDescription(
                sharedLayout,
                sc.ProjectionMatrixBuffer,
                sc.ViewMatrixBuffer,
                sc.LightInfoBuffer,
                _worldInfoBuffer,
                _textureAtlasBuffer));

            foreach (ChunkVisual visual in _visuals)
                visual.CreateDeviceObjects(gd, cl, sc);
        }

        public override void DestroyDeviceObjects()
        {
            foreach (ChunkVisual visual in _visuals)
                visual.DestroyDeviceObjects();

            _disposeCollector.DisposeAll();
        }

        public void Update(in FrameTime time)
        {
            _worldInfo.GlobalTime = time.TotalSeconds;
            _worldInfo.GlobalTimeFraction = _worldInfo.GlobalTime - MathF.Floor(_worldInfo.GlobalTime);
        }

        public void GatherVisibleChunks(List<ChunkVisual> visibleChunks)
        {
            visibleChunks.AddRange(_visuals);
        }

        public void Render(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            List<ChunkVisual> visibleChunks = _visibleChunkBuffer;
            visibleChunks.Clear();
            GatherVisibleChunks(visibleChunks);

            cl.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _sharedSet);

            foreach (ChunkVisual visual in visibleChunks)
            {
                visual.Render(gd, cl);
            }
        }

        public void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ReadOnlySpan<Matrix4x4> projView = stackalloc Matrix4x4[] { Camera.ProjectionMatrix, Camera.ViewMatrix };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldInfo
    {
        public float GlobalTime;
        public float GlobalTimeFraction;
        
        private float _padding1;
        private float _padding2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldAndInverse
    {
        public Matrix4x4 World;
        public Matrix4x4 InverseWorld;
    }

    public class ChunkVisual : GraphicsResource
    {
        private ChunkSpaceVertex[] _spaceVertices;
        private ChunkPaintVertex[] _paintVertices;
        private ushort[] _indices;

        private DeviceBuffer _worldAndInverseBuffer;
        private DeviceBuffer _spaceBuffer;
        private DeviceBuffer _paintBuffer;
        private DeviceBuffer _indexBuffer;

        private ResourceSet _chunkInfoSet;

        public ChunkRenderer Renderer { get; }

        public ChunkVisual(ChunkRenderer chunkRenderer)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));

            _spaceVertices = GetSpaceVertices();
            _paintVertices = GetPaintVertices();
            _indices = GetCubeIndices();
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _worldAndInverseBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<WorldAndInverse>(), BufferUsage.UniformBuffer));

            _chunkInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                Renderer.ChunkInfoLayout,
                _worldAndInverseBuffer));

            _spaceBuffer = factory.CreateBuffer(new BufferDescription(_spaceVertices.SizeInBytes(), BufferUsage.VertexBuffer));
            gd.UpdateBuffer(_spaceBuffer, 0, _spaceVertices);

            _paintBuffer = factory.CreateBuffer(new BufferDescription(_paintVertices.SizeInBytes(), BufferUsage.VertexBuffer));
            gd.UpdateBuffer(_paintBuffer, 0, _paintVertices);

            _indexBuffer = factory.CreateBuffer(new BufferDescription(_indices.SizeInBytes(), BufferUsage.IndexBuffer));
            gd.UpdateBuffer(_indexBuffer, 0, _indices);
        }

        public override void DestroyDeviceObjects()
        {
            _worldAndInverseBuffer.Dispose();
        }

        public void Render(GraphicsDevice gd, CommandList cl)
        {
            WorldAndInverse worldAndInverse;
            worldAndInverse.World = Matrix4x4.CreateScale(10) * Matrix4x4.CreateTranslation(0, 10, 0);
            worldAndInverse.InverseWorld = VdUtilities.CalculateInverseTranspose(ref worldAndInverse.World);
            cl.UpdateBuffer(_worldAndInverseBuffer, 0, ref worldAndInverse);

            cl.SetGraphicsResourceSet(1, _chunkInfoSet);

            cl.SetVertexBuffer(0, _spaceBuffer);
            cl.SetVertexBuffer(1, _paintBuffer);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(36, 1, 0, 0, 0);
        }

        private static ChunkSpaceVertex[] GetSpaceVertices()
        {
            var vertices = new ChunkSpaceVertex[]
            {
                // Top
                new (new Vector3(-0.5f, +0.5f, -0.5f), Vector3.UnitY),
                new (new Vector3(+0.5f, +0.5f, -0.5f), Vector3.UnitY),
                new (new Vector3(+0.5f, +0.5f, +0.5f), Vector3.UnitY),
                new (new Vector3(-0.5f, +0.5f, +0.5f), Vector3.UnitY),
                // Bottom                                             
                new (new Vector3(-0.5f,-0.5f, +0.5f), -Vector3.UnitY),
                new (new Vector3(+0.5f,-0.5f, +0.5f), -Vector3.UnitY),
                new (new Vector3(+0.5f,-0.5f, -0.5f), -Vector3.UnitY),
                new (new Vector3(-0.5f,-0.5f, -0.5f), -Vector3.UnitY),
                // Left                                               
                new (new Vector3(-0.5f, +0.5f, -0.5f), -Vector3.UnitX),
                new (new Vector3(-0.5f, +0.5f, +0.5f), -Vector3.UnitX),
                new (new Vector3(-0.5f, -0.5f, +0.5f), -Vector3.UnitX),
                new (new Vector3(-0.5f, -0.5f, -0.5f), -Vector3.UnitX),
                // Right                                              
                new (new Vector3(+0.5f, +0.5f, +0.5f), Vector3.UnitX),
                new (new Vector3(+0.5f, +0.5f, -0.5f), Vector3.UnitX),
                new (new Vector3(+0.5f, -0.5f, -0.5f), Vector3.UnitX),
                new (new Vector3(+0.5f, -0.5f, +0.5f), Vector3.UnitX),
                // Back                                               
                new (new Vector3(+0.5f, +0.5f, -0.5f), -Vector3.UnitZ),
                new (new Vector3(-0.5f, +0.5f, -0.5f), -Vector3.UnitZ),
                new (new Vector3(-0.5f, -0.5f, -0.5f), -Vector3.UnitZ),
                new (new Vector3(+0.5f, -0.5f, -0.5f), -Vector3.UnitZ),
                // Front                                              
                new (new Vector3(-0.5f, +0.5f, +0.5f), Vector3.UnitZ),
                new (new Vector3(+0.5f, +0.5f, +0.5f), Vector3.UnitZ),
                new (new Vector3(+0.5f, -0.5f, +0.5f), Vector3.UnitZ),
                new (new Vector3(-0.5f, -0.5f, +0.5f), Vector3.UnitZ),
            };
            return vertices;
        }

        private static ChunkPaintVertex[] GetPaintVertices()
        {
            var vertices = new ChunkPaintVertex[]
            {
                // Top
                new (RgbaByte.Red  , 1, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.White, 1, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 1, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 1, 0), // new Vector2U16(0, 1)),
                // Bottom
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 1)),
                // Left
                new (RgbaByte.Green, 0, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 1)),
                // Right
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 1)),
                // Back
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.Blue , 0, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 1)),
                // Front
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 0)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(1, 1)),
                new (RgbaByte.White, 0, 0), // new Vector2U16(0, 1)),
            };

            return vertices;
        }

        private static ushort[] GetCubeIndices()
        {
            ushort[] indices =
            {
                0,1,2, 0,2,3,
                4,5,6, 4,6,7,
                8,9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23,
            };

            return indices;
        }

        public struct ChunkSpaceVertex
        {
            public HalfVector4 Position;
            public uint Normal;

            public ChunkSpaceVertex(HalfVector4 position, uint normal)
            {
                Position = position;
                Normal = normal;
            }

            public ChunkSpaceVertex(Vector3 position, Vector3 normal)
            {
                normal += Vector3.One;
                normal *= 1023f / 2f;

                this = CreateFromNormalized(new HalfVector4(position, 1f), normal);
            }

            public static ChunkSpaceVertex CreateFromNormalized(HalfVector4 position, Vector3 normal)
            {
                uint nx = (uint)normal.X;
                uint ny = (uint)normal.Y;
                uint nz = (uint)normal.Z;
                return new ChunkSpaceVertex(position, Pack(nx, ny, nz));
            }

            public static uint Pack(uint x, uint y, uint z)
            {
                return x | (y << 10) | (z << 20);
            }
        }

        public struct ChunkPaintVertex
        {
            public RgbaByte Color;
            public uint TexAnimation0;
            public uint TexRegion0;

            public ChunkPaintVertex(RgbaByte color, uint texAnimation0, uint texRegion0)
            {
                Color = color;
                TexAnimation0 = texAnimation0;
                TexRegion0 = texRegion0;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TextureRegion
    {
        public uint XY;
        public uint TextureAndRgb;

        public TextureRegion(ushort x, ushort y, byte texture, byte r, byte g, byte b)
        {
            XY = x | (uint)y << 16;
            TextureAndRgb = texture | (uint)r << 8 | (uint)g << 16 | (uint)b << 24;
        }
    }
}
