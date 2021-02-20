using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
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

        private ConcurrentQueue<ChunkVisual> _queuedCreations = new();

        private WorldInfo _worldInfo;

        private uint _lastTriangleCount;
        private uint _lastChunkCount;

        public Camera Camera { get; }

        public ResourceLayout ChunkInfoLayout { get; private set; }

        public ChunkRenderer(Camera camera)
        {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));

            int width = 16;
            int height = 8;

            Task.Run(() =>
            {
                var list = new List<(int x, int y, int z)>();

                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < width; z++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            list.Add((x, y, z));
                        }
                    }
                }

                list.Sort((a, b) =>
                {
                    int cx = width / 2;
                    int cy = height / 2;
                    int cz = width / 2;

                    int ax = (cx - a.x);
                    int ay = (cy - a.y);
                    int az = (cz - a.z);
                    int av = ax * ax + ay * ay + az * az;

                    int bx = (cx - b.x);
                    int by = (cy - b.y);
                    int bz = (cz - b.z);
                    int bv = bx * bx + by * by + bz * bz;

                    return av.CompareTo(bv);
                });

                int count = 0;
                foreach (var (x, y, z) in list)
                {
                    var visual = new ChunkVisual(this, x, y, z);

                    _queuedCreations.Enqueue(visual);

                    count++;
                    if (count == 2)
                    {
                        Thread.Sleep(1);
                        count = 0;
                    }
                }
            });
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
                    new ResourceLayoutElementDescription("WorldInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("LightInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("TextureAtlas", ResourceKind.StructuredBufferReadOnly, ShaderStages.Fragment)));

            ChunkInfoLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ChunkInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            VertexLayoutDescription spaceLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription paintLayout = new VertexLayoutDescription(
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
                new TextureRegion(0, 255, 100, 100, 0, 0),
                new TextureRegion(0, 100, 255, 100, 0, 0),
                new TextureRegion(0, 100, 100, 255, 0, 0),
            };
            _textureAtlasBuffer = factory.CreateBuffer(new BufferDescription(
                regions.SizeInBytes(), BufferUsage.StructuredBufferReadOnly, (uint)Unsafe.SizeOf<TextureRegion>(), true));
            gd.UpdateBuffer(_textureAtlasBuffer, 0, regions);

            _sharedSet = factory.CreateResourceSet(new ResourceSetDescription(
                sharedLayout,
                sc.ProjectionMatrixBuffer,
                sc.ViewMatrixBuffer,
                _worldInfoBuffer,
                sc.LightInfoBuffer,
                _textureAtlasBuffer));

            int cc = 1;
            _commandLists = new CommandList[cc];
            _tasks = new Task[cc];
            _triangleCounts = new uint[cc];
            _chunkCounts = new uint[cc];

            for (int i = 0; i < _commandLists.Length; i++)
            {
                _commandLists[i] = factory.CreateCommandList();
            }

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

            ImGuiNET.ImGui.Begin("Stats");
            {
                ImGuiNET.ImGui.Text("Triangle Count: " + (_lastTriangleCount / 1000) + "k");
                ImGuiNET.ImGui.Text("Chunk Count: " + _lastChunkCount);
            }
            ImGuiNET.ImGui.End();
        }

        public void GatherVisibleChunks(List<ChunkVisual> visibleChunks)
        {
            visibleChunks.AddRange(_visuals);
        }

        private CommandList[] _commandLists;
        private Task?[] _tasks;
        private uint[] _triangleCounts;
        private uint[] _chunkCounts;

        public void Render(GraphicsDevice gd, SceneContext sc)
        {
            var commandLists = _commandLists;
            gd.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

            var tmpCommands = commandLists[0];
            tmpCommands.Begin();
            while (_queuedCreations.TryDequeue(out ChunkVisual? visual))
            {
                visual.CreateDeviceObjects(gd, tmpCommands, sc);
                _visuals.Add(visual);
            }
            tmpCommands.End();
            gd.SubmitCommands(tmpCommands);

            List<ChunkVisual> visibleChunks = _visibleChunkBuffer;
            visibleChunks.Clear();
            GatherVisibleChunks(visibleChunks);

            _lastTriangleCount = 0;
            _lastChunkCount = 0;

            int visibleChunkOffset = 0;
            float fbWidth = sc.MainSceneFramebuffer.Width;
            float fbHeight = sc.MainSceneFramebuffer.Height;
            Viewport viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1f);

            int chunksPerTask = (visibleChunks.Count + commandLists.Length) / commandLists.Length;

            for (int i = 0; i < commandLists.Length; i++)
            {
                int index = i;
                _triangleCounts[index] = 0;
                _chunkCounts[index] = 0;

                int start = visibleChunkOffset;
                int end = Math.Min(visibleChunks.Count, visibleChunkOffset + chunksPerTask);
                int count = end - start;
                if (count == 0)
                {
                    _tasks[index] = null;
                    break;
                }
                visibleChunkOffset += count;

                CommandList commands = commandLists[index];
                commands.Begin();
                _tasks[index] = Task.Run(() =>
                {
                    commands.SetPipeline(_pipeline);
                    commands.SetGraphicsResourceSet(0, _sharedSet);
                    commands.SetFramebuffer(sc.MainSceneFramebuffer);
                    commands.SetViewport(0, ref viewport);
                    sc.UpdateCameraBuffers(commands); // Re-set because reflection step changed it.

                    for (int j = start; j < end; j++)
                    {
                        ChunkVisual? visual = visibleChunks[j];
                        visual.Render(gd, commands);

                        uint triCount = visual.TriangleCount;
                        _triangleCounts[index] += triCount;
                        if (triCount > 0)
                            _chunkCounts[index]++;
                    }
                });
            }

            for (int i = 0; i < _tasks.Length; i++)
            {
                var task = _tasks[i];
                if (task != null)
                {
                    task.Wait();
                    commandLists[i].End();
                    _lastTriangleCount += _triangleCounts[i];
                    _lastChunkCount += _chunkCounts[i];
                }
            }

            for (int i = 0; i < commandLists.Length; i++)
            {
                CommandList commands = commandLists[i];
                gd.SubmitCommands(commands);
            }

            Debug.Assert(visibleChunkOffset == visibleChunks.Count);
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
        private DeviceBuffer _worldAndInverseBuffer;
        private ResourceSet _chunkInfoSet;

        private DeviceBuffer _spaceBuffer;
        private DeviceBuffer _paintBuffer;
        private DeviceBuffer? _indexBuffer;

        public ChunkRenderer Renderer { get; }

        public int ChunkX { get; }
        public int ChunkY { get; }
        public int ChunkZ { get; }

        public uint TriangleCount => _indexBuffer == null ? 0 : (_indexBuffer.SizeInBytes / 4) / 3;

        public ChunkVisual(ChunkRenderer chunkRenderer, int chunkX, int chunkY, int chunkZ)
        {
            Renderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));

            ChunkX = chunkX;
            ChunkY = chunkY;
            ChunkZ = chunkZ;
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _worldAndInverseBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<WorldAndInverse>(), BufferUsage.UniformBuffer));

            _chunkInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                Renderer.ChunkInfoLayout,
                _worldAndInverseBuffer));

            var go = new Mesher();
            var mesh = go.Mesh(this);

            if (mesh.Indices.Count != 0)
            {
                ReadOnlySpan<uint> indicesSpan = mesh.Indices.Span;
                _indexBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(indicesSpan).Length, BufferUsage.IndexBuffer));
                gd.UpdateBuffer(_indexBuffer, 0, indicesSpan);

                ReadOnlySpan<ChunkSpaceVertex> spaceVertexSpan = mesh.SpaceVertices.Span;
                _spaceBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(spaceVertexSpan).Length, BufferUsage.VertexBuffer));
                gd.UpdateBuffer(_spaceBuffer, 0, spaceVertexSpan);

                ReadOnlySpan<ChunkPaintVertex> paintVertexSpan = mesh.PaintVertices.Span;
                _paintBuffer = factory.CreateBuffer(new BufferDescription((uint)MemoryMarshal.AsBytes(paintVertexSpan).Length, BufferUsage.VertexBuffer));
                gd.UpdateBuffer(_paintBuffer, 0, paintVertexSpan);

                WorldAndInverse worldAndInverse;
                worldAndInverse.World = Matrix4x4.CreateTranslation(ChunkX * 16, ChunkY * 16, ChunkZ * 16);
                worldAndInverse.InverseWorld = VdUtilities.CalculateInverseTranspose(ref worldAndInverse.World);
                gd.UpdateBuffer(_worldAndInverseBuffer, 0, ref worldAndInverse);
            }

            mesh.Dispose();
        }

        public override void DestroyDeviceObjects()
        {
            _worldAndInverseBuffer?.Dispose();
            _chunkInfoSet?.Dispose();

            _indexBuffer?.Dispose();
            _indexBuffer = null;
            _spaceBuffer?.Dispose();
            _paintBuffer?.Dispose();
        }

        public void Render(GraphicsDevice gd, CommandList cl)
        {
            if (_indexBuffer == null)
                return;

            cl.SetGraphicsResourceSet(1, _chunkInfoSet);

            cl.SetVertexBuffer(0, _spaceBuffer);
            cl.SetVertexBuffer(1, _paintBuffer);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt32);
            cl.DrawIndexed(_indexBuffer.SizeInBytes / 4, 1, 0, 0, 0);
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
            var anim0 = TextureAnimation.Create(TextureAnimationType.Step, 3, 0.5f);
            var anim1 = TextureAnimation.Create(TextureAnimationType.MixStep, 3, 0.5f);

            var vertices = new ChunkPaintVertex[]
            {
                // Top
                new (anim0, 0), // new Vector2U16(0, 0)),
                new (anim0, 0), // new Vector2U16(1, 0)),
                new (anim0, 0), // new Vector2U16(1, 1)),
                new (anim0, 0), // new Vector2U16(0, 1)),
                // Bottom
                new (anim1, 0), // new Vector2U16(0, 0)),
                new (anim1, 0), // new Vector2U16(1, 0)),
                new (anim1, 0), // new Vector2U16(1, 1)),
                new (anim1, 0), // new Vector2U16(0, 1)),
                // Left
                new (anim1, 0), // new Vector2U16(0, 0)),
                new (anim1, 0), // new Vector2U16(1, 0)),
                new (anim1, 0), // new Vector2U16(1, 1)),
                new (anim1, 0), // new Vector2U16(0, 1)),
                // Right
                new (anim1, 0), // new Vector2U16(0, 0)),
                new (anim1, 0), // new Vector2U16(1, 0)),
                new (anim1, 0), // new Vector2U16(1, 1)),
                new (anim1, 0), // new Vector2U16(0, 1)),
                // Back
                new (anim1, 0), // new Vector2U16(0, 0)),
                new (anim1, 0), // new Vector2U16(1, 0)),
                new (anim1, 0), // new Vector2U16(1, 1)),
                new (anim1, 0), // new Vector2U16(0, 1)),
                // Front
                new (anim1, 0), // new Vector2U16(0, 0)),
                new (anim1, 0), // new Vector2U16(1, 0)),
                new (anim1, 0), // new Vector2U16(1, 1)),
                new (anim1, 0), // new Vector2U16(0, 1)),
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
            public Vector3 Position;
            public uint Normal;

            public ChunkSpaceVertex(Vector3 position, uint normal)
            {
                Position = position;
                Normal = normal;
            }

            public ChunkSpaceVertex(Vector4 position, uint normal)
            {
                Position = Unsafe.As<Vector4, Vector3>(ref position);
                Normal = normal;
            }

            public ChunkSpaceVertex(Vector3 position, Vector3 normal) : this(position, PackNormal(normal))
            {
            }

            public ChunkSpaceVertex(Vector4 position, Vector4 normal) : this(position, PackNormal(normal))
            {
            }

            public static uint Pack(uint x, uint y, uint z)
            {
                return x | (y << 10) | (z << 20);
            }

            public static uint Pack(Vector3 vector)
            {
                uint nx = (uint)vector.X;
                uint ny = (uint)vector.Y;
                uint nz = (uint)vector.Z;
                return Pack(nx, ny, nz);
            }

            public static uint PackNormal(Vector4 normal)
            {
                normal += Vector4.One;
                normal *= 1023f / 2f;
                return Pack(Unsafe.As<Vector4, Vector3>(ref normal));
            }

            public static uint PackNormal(Vector3 normal)
            {
                normal += Vector3.One;
                normal *= 1023f / 2f;
                return Pack(normal);
            }
        }

        public struct ChunkPaintVertex
        {
            public TextureAnimation TexAnimation0;
            public uint TexRegion0;

            public ChunkPaintVertex(TextureAnimation texAnimation0, uint texRegion0)
            {
                TexAnimation0 = texAnimation0;
                TexRegion0 = texRegion0;
            }
        }
    }

    public class Mesher
    {
        public ref struct StoredChunkMesh
        {
            public ByteStore<uint> Indices;
            public ByteStore<ChunkVisual.ChunkSpaceVertex> SpaceVertices;
            public ByteStore<ChunkVisual.ChunkPaintVertex> PaintVertices;

            public StoredChunkMesh(
                ByteStore<uint> indices, 
                ByteStore<ChunkVisual.ChunkSpaceVertex> spaceVertices, 
                ByteStore<ChunkVisual.ChunkPaintVertex> paintVertices)
            {
                Indices = indices;
                SpaceVertices = spaceVertices;
                PaintVertices = paintVertices;
            }

            public void Dispose()
            {
                Indices.Dispose();
                SpaceVertices.Dispose();
                PaintVertices.Dispose();
            }
        }

        public StoredChunkMesh Mesh(ChunkVisual visual)
        {
            var ind = new ByteStore<uint>                        (ArrayPool<byte>.Shared);
            var spa = new ByteStore<ChunkVisual.ChunkSpaceVertex>(ArrayPool<byte>.Shared);
            var pai = new ByteStore<ChunkVisual.ChunkPaintVertex>(ArrayPool<byte>.Shared);

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

            var rng = new Random();

            for (int y = 0; y < 16; y++)
            {
                for (int z = 0; z < 16; z++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        double fac = (visual.ChunkY * 16 + y) / 2048.0;
                        if (rng.NextDouble() > 0.1 * fac)
                            continue;

                        var spaGen = new CubeSpaceVertexGenerator(new Vector3(x, y, z));

                        var anim = anims[rng.Next(anims.Length)];
                        var paiGen = new CubePaintVertexGenerator(anim, 0);

                        var spaPro = new CubeElementProvider<CubeSpaceVertexGenerator, ChunkVisual.ChunkSpaceVertex>(spaGen, CubeFaces.All);
                        var paiPro = new CubeElementProvider<CubePaintVertexGenerator, ChunkVisual.ChunkPaintVertex>(paiGen, spaPro.Faces);

                        spaPro.AppendElements(ref spa);
                        paiPro.AppendElements(ref pai);
                        indPro.AppendIndices(ref ind, ref vertexOffset);
                    }
                }
            }

            return new StoredChunkMesh(ind, spa, pai);
        }
    }

    public struct CubeSpaceVertexGenerator : ICubeElementGenerator<ChunkVisual.ChunkSpaceVertex>
    {
        public static uint BackNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(-Vector3.UnitZ);
        public static uint BottomNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(-Vector3.UnitY);
        public static uint FrontNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(Vector3.UnitZ);
        public static uint FeftNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(-Vector3.UnitX);
        public static uint RightNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(Vector3.UnitX);
        public static uint TopNormal { get; } = ChunkVisual.ChunkSpaceVertex.PackNormal(Vector3.UnitY);

        public int MaxElementsPerBlock => 4 * 6;

        public Vector3 Position { get; }

        public CubeSpaceVertexGenerator(Vector3 position)
        {
            Position = position;
        }

        public void AppendBack(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, -0.5f) + Position, BackNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, -0.5f) + Position, BackNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, -0.5f) + Position, BackNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, -0.5f) + Position, BackNormal));
        }

        public void AppendBottom(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, +0.5f) + Position, BottomNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, +0.5f) + Position, BottomNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, -0.5f) + Position, BottomNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, -0.5f) + Position, BottomNormal));
        }

        public void AppendFront(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, +0.5f) + Position, FrontNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, +0.5f) + Position, FrontNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, +0.5f) + Position, FrontNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, +0.5f) + Position, FrontNormal));
        }

        public void AppendLeft(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, -0.5f) + Position, FeftNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, +0.5f) + Position, FeftNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, +0.5f) + Position, FeftNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, -0.5f, -0.5f) + Position, FeftNormal));
        }

        public void AppendRight(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, +0.5f) + Position, RightNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, -0.5f) + Position, RightNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, -0.5f) + Position, RightNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, -0.5f, +0.5f) + Position, RightNormal));
        }

        public void AppendTop(ref ByteStore<ChunkVisual.ChunkSpaceVertex> store)
        {
            store.AppendRange(
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, -0.5f) + Position, TopNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, -0.5f) + Position, TopNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(+0.5f, +0.5f, +0.5f) + Position, TopNormal),
                new ChunkVisual.ChunkSpaceVertex(new Vector3(-0.5f, +0.5f, +0.5f) + Position, TopNormal));
        }
    }

    public struct CubePaintVertexGenerator : ICubeElementGenerator<ChunkVisual.ChunkPaintVertex>
    {
        public int MaxElementsPerBlock => 4 * 6;

        public TextureAnimation TextureAnimation { get; }
        public uint TextureRegion { get; }

        public CubePaintVertexGenerator(TextureAnimation textureAnimation, uint textureRegion)
        {
            TextureAnimation = textureAnimation;
            TextureRegion = textureRegion;
        }

        public void AppendBack(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendBottom(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendFront(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendLeft(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendRight(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }

        public void AppendTop(ref ByteStore<ChunkVisual.ChunkPaintVertex> store)
        {
            var vertex = new ChunkVisual.ChunkPaintVertex(TextureAnimation, TextureRegion);
            store.AppendRange(vertex, vertex, vertex, vertex);
        }
    }

    public struct CubeIndexGenerator : ICubeIndexGenerator<uint>
    {
        public int MaxIndicesPerBlock => 6 * 6;

        public static void AppendQuad(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            store.AppendRange(
                vertexOffset,
                vertexOffset + 1,
                vertexOffset + 2,
                vertexOffset,
                vertexOffset + 2,
                vertexOffset + 3);

            vertexOffset += 4;
        }

        public void AppendBack(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendBottom(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendFront(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendLeft(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendRight(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }

        public void AppendTop(ref ByteStore<uint> store, ref uint vertexOffset)
        {
            AppendQuad(ref store, ref vertexOffset);
        }
    }

    public struct CubeElementProvider<TGenerator, T> : IElementGenerator<T>
        where TGenerator : ICubeElementGenerator<T>
        where T : unmanaged
    {
        public TGenerator Generator { get; }
        public CubeFaces Faces { get; }

        public CubeElementProvider(TGenerator generator, CubeFaces faces)
        {
            Debug.Assert(generator != null);

            Generator = generator;
            Faces = faces;
        }

        public void AppendElements(ref ByteStore<T> store)
        {
            store.PrepareCapacity(Generator.MaxElementsPerBlock);

            if ((Faces & CubeFaces.Top) != 0)
                Generator.AppendTop(ref store);

            if ((Faces & CubeFaces.Bottom) != 0)
                Generator.AppendBottom(ref store);

            if ((Faces & CubeFaces.Left) != 0)
                Generator.AppendLeft(ref store);

            if ((Faces & CubeFaces.Right) != 0)
                Generator.AppendRight(ref store);

            if ((Faces & CubeFaces.Front) != 0)
                Generator.AppendFront(ref store);

            if ((Faces & CubeFaces.Back) != 0)
                Generator.AppendBack(ref store);
        }
    }

    public struct CubeIndexProvider<TGenerator, T> : IIndexGenerator<T>
        where TGenerator : ICubeIndexGenerator<T>
        where T : unmanaged
    {
        public TGenerator Generator { get; }
        public CubeFaces Faces { get; }

        public CubeIndexProvider(TGenerator generator, CubeFaces faces)
        {
            Debug.Assert(generator != null);

            Generator = generator;
            Faces = faces;
        }

        public void AppendIndices(ref ByteStore<T> store, ref uint vertexOffset)
        {
            store.PrepareCapacity(Generator.MaxIndicesPerBlock);

            if ((Faces & CubeFaces.Top) != 0)
                Generator.AppendTop(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Bottom) != 0)
                Generator.AppendBottom(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Left) != 0)
                Generator.AppendLeft(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Right) != 0)
                Generator.AppendRight(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Front) != 0)
                Generator.AppendFront(ref store, ref vertexOffset);

            if ((Faces & CubeFaces.Back) != 0)
                Generator.AppendBack(ref store, ref vertexOffset);
        }
    }

    public interface IElementGenerator<T>
        where T : unmanaged
    {
        void AppendElements(ref ByteStore<T> store);
    }

    public interface IIndexGenerator<T>
        where T : unmanaged
    {
        void AppendIndices(ref ByteStore<T> store, ref uint vertexOffset);
    }

    public interface ICubeElementGenerator<T>
        where T : unmanaged
    {
        int MaxElementsPerBlock { get; }

        void AppendTop(ref ByteStore<T> store);
        void AppendBottom(ref ByteStore<T> store);
        void AppendLeft(ref ByteStore<T> store);
        void AppendRight(ref ByteStore<T> store);
        void AppendFront(ref ByteStore<T> store);
        void AppendBack(ref ByteStore<T> store);
    }

    public interface ICubeIndexGenerator<T>
       where T : unmanaged
    {
        int MaxIndicesPerBlock { get; }

        void AppendTop(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendBottom(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendLeft(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendRight(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendFront(ref ByteStore<T> store, ref uint vertexOffset);
        void AppendBack(ref ByteStore<T> store, ref uint vertexOffset);
    }

    [Flags]
    public enum CubeFaces
    {
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Front = 1 << 4,
        Back = 1 << 5,

        All = Top | Bottom | Left | Right | Front | Back
    }
}
