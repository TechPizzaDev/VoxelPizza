using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public class ChunkRenderer : GraphicsResource, IUpdateable
    {
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
                    sc.UpdateCameraBuffers(commands);

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
                Task? task = _tasks[i];
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
}
