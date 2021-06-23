using System;
using System.Buffers;
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
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    // TODO: separate commonly updated chunks into singular mesh instances

    public class ChunkRenderer : Renderable, IUpdateable
    {
        /// <summary>
        /// The amount of blocks that are fetched around a chunk for meshing.
        /// </summary>
        public const int FetchBlockMargin = 2;

        private DisposeCollector _disposeCollector;
        private string _graphicsDeviceName;
        private string _graphicsBackendName;

        private bool[] _uploadReady;
        private CommandList[] _uploadLists;
        private Fence[] _uploadFences;
        private ChunkStagingMeshPool _stagingMeshPool;
        private List<ChunkStagingMesh>[] _uploadSubmittedMeshes;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;

        private Pipeline _directPipeline;
        private Pipeline _indirectPipeline;
        private ResourceSet _sharedSet;

        private Dictionary<ChunkPosition, Chunk> _chunks = new();
        private AutoResetEvent frameEvent = new(false);

        private List<ChunkMesh> _visibleMeshBuffer = new();
        private List<ChunkMesh> _meshes = new();
        private ConcurrentQueue<ChunkMesh> _queuedMeshes = new();

        private List<ChunkMeshRegion> _visibleRegionBuffer = new();
        private Dictionary<ChunkRegionPosition, ChunkMeshRegion> _regions = new();
        private ConcurrentQueue<ChunkMeshRegion> _queuedRegions = new();

        private WorldInfo _worldInfo;

        private int _lastTriangleCount;
        private int _lastDrawCalls;

        public Size3 RegionSize { get; }
        public HeapPool ChunkMeshPool { get; }
        public ChunkMesher ChunkMesher { get; }

        public ResourceLayout ChunkInfoLayout { get; private set; }

        public Camera? RenderCamera { get; set; }
        public Camera? BoundCamera { get; set; }

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public event Action<Chunk>? ChunkAdded;
        public event Action<ChunkMesh>? ChunkMeshAdded;
        public event Action<ChunkMeshRegion>? ChunkRegionAdded;

        public event Action<Chunk>? ChunkRemoved;
        public event Action<ChunkMesh>? ChunkMeshRemoved;
        public event Action<ChunkMeshRegion>? ChunkRegionRemoved;

        public ChunkRenderer(Size3 regionSize)
        {
            RegionSize = regionSize;
            ChunkMeshPool = new HeapPool(1024 * 1024 * 16);
            ChunkMesher = new ChunkMesher(ChunkMeshPool);

            _uploadReady = new bool[4];
            _uploadLists = new CommandList[_uploadReady.Length];
            _uploadFences = new Fence[_uploadReady.Length];
            _uploadSubmittedMeshes = new List<ChunkStagingMesh>[_uploadReady.Length];

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadSubmittedMeshes[i] = new List<ChunkStagingMesh>();
            }

            StartThread();
        }

        public ChunkRegionPosition GetRegionPosition(ChunkPosition chunkPosition)
        {
            return new ChunkRegionPosition(
                IntMath.DivideRoundDown(chunkPosition.X, (int)RegionSize.W),
                IntMath.DivideRoundDown(chunkPosition.Y, (int)RegionSize.H),
                IntMath.DivideRoundDown(chunkPosition.Z, (int)RegionSize.D));
        }

        public void StartThread()
        {
            Task.Run(() =>
            {
                try
                {
                    //Thread.Sleep(2000);

                    int width = 16;
                    int depth = width;
                    int height = 1;

                    var list = new List<(int x, int y, int z)>();

                    int off = 0;
                    for (int y = -off; y < height; y++)
                    {
                        for (int z = 0; z < depth; z++)
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
                        int cz = depth / 2;

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
                        var chunk = new Chunk(new(x, y, z));
                        ChunkAdded?.Invoke(chunk);

                        if (y < 0)
                            chunk.SetBlockLayer(15, 1);
                        else
                            chunk.Generate();

                        lock (_chunks)
                            _chunks.Add(chunk.Position, chunk);

                        ChunkRegionPosition regionPosition = GetRegionPosition(chunk.Position);
                        ChunkMeshRegion? region;
                        lock (_regions)
                        {
                            if (!_regions.TryGetValue(regionPosition, out region))
                            {
                                region = new ChunkMeshRegion(this, regionPosition, RegionSize);
                                _regions.Add(regionPosition, region);
                                _queuedRegions.Enqueue(region);
                            }
                        }

                        //region.UpdateChunk(chunk);

                        //var mesh = new ChunkMesh(this, chunk);
                        //_queuedMeshes.Enqueue(mesh);

                        count++;
                        if (count == 1)
                        {
                            //Thread.Sleep(1);
                            count = 0;
                        }
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int z = off; z < depth - off; z++)
                        {
                            for (int x = off; x < width - off; x++)
                            {
                                var pp = new ChunkPosition(x, y, z);
                                ChunkRegionPosition regionPosition = GetRegionPosition(pp);
                                Chunk? chunk = GetChunk(pp);
                                _regions[regionPosition].UpdateChunk(chunk);
                            }
                        }
                    }

                    return;

                    Random rng = new Random(1234);
                    for (int i = 0; i < (64 * 1024) / (width * height * depth); i++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int z = 0; z < depth; z++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int xd = rng.Next(2);
                                    int max = xd == 0 ? 512 : 128;
                                    if (_chunks.TryGetValue(new ChunkPosition(x, y, z), out Chunk? c))
                                    {
                                        uint[] blocks = c.Blocks;
                                        for (int b = 0; b < blocks.Length; b++)
                                        {
                                            blocks[b] = (uint)rng.Next(max);
                                        }
                                        //blocks.AsSpan().Clear();

                                        ChunkRegionPosition regionPosition = GetRegionPosition(c.Position);
                                        _regions[regionPosition].UpdateChunk(c);
                                    }
                                }
                            }
                        }

                        frameEvent.WaitOne();
                        frameEvent.WaitOne();
                        //frameEvent.WaitOne();
                        //frameEvent.WaitOne();
                    }

                    frameEvent.WaitOne();
                    Thread.Sleep(1000);
                    Environment.Exit(0);

                    return;
                    Thread.Sleep(5000);

                    while (true)
                    {
                        int x = rng.Next(width);
                        int z = rng.Next(depth);
                        int y = rng.Next(height);
                        if (_chunks.TryGetValue(new ChunkPosition(x, y, z), out Chunk? c))
                        {
                            c.Blocks[rng.Next(c.Blocks.Length)] = 0;

                            ChunkRegionPosition regionPosition = GetRegionPosition(c.Position);
                            _regions[regionPosition].UpdateChunk(c);

                            Thread.Sleep(10);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }

        public Chunk? GetChunk(ChunkPosition position)
        {
            Chunk? chunk;
            lock (_chunks)
            {
                _chunks.TryGetValue(position, out chunk);
            }
            return chunk;
        }

        public Chunk? GetChunk(int x, int y, int z)
        {
            return GetChunk(new ChunkPosition(x, y, z));
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _graphicsDeviceName = gd.DeviceName;
            _graphicsBackendName = gd.BackendType.ToString();

            DisposeCollectorResourceFactory factory = new(gd.ResourceFactory);
            _disposeCollector = factory.DisposeCollector;

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadReady[i] = true;
                _uploadLists[i] = factory.CreateCommandList();
                _uploadFences[i] = factory.CreateFence(false);
                _uploadSubmittedMeshes[i].Clear();
            }

            _stagingMeshPool = new ChunkStagingMeshPool(factory, RegionSize.Volume);

            ResourceLayout sharedLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("WorldInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("LightInfo", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("TextureAtlas", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)));

            ChunkInfoLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ChunkInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            VertexLayoutDescription spaceLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription paintLayout = new VertexLayoutDescription(
                new VertexElementDescription("TexAnimation0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
                new VertexElementDescription("TexRegion0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription worldLayout = new VertexLayoutDescription(
                new VertexElementDescription("Translation", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3))
            {
                InstanceStepRate = 1
            };

            (Shader mainVs, Shader mainFs, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkMain");

            var rasterizerState = RasterizerStateDescription.Default;
            //rasterizerState.CullMode = FaceCullMode.None;
            //rasterizerState.FillMode = PolygonFillMode.Wireframe;

            var depthStencilState = gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual;

            _directPipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                depthStencilState,
                rasterizerState,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { spaceLayout, paintLayout },
                    new[] { mainVs, mainFs, },
                    mainSpecs),
                new[] { sc.CameraInfoLayout, sharedLayout, ChunkInfoLayout },
                sc.MainSceneFramebuffer.OutputDescription));

            (Shader mainIndirectVs, Shader mainIndirectFs, SpecializationConstant[] mainIndirectSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkIndirectMain", "ChunkMain");

            _indirectPipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                depthStencilState,
                rasterizerState,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { spaceLayout, paintLayout, worldLayout },
                    new[] { mainIndirectVs, mainIndirectFs, },
                    mainIndirectSpecs),
                new[] { sc.CameraInfoLayout, sharedLayout },
                sc.MainSceneFramebuffer.OutputDescription));

            _worldInfoBuffer = factory.CreateBuffer(new BufferDescription(
                (uint)Unsafe.SizeOf<WorldInfo>(), BufferUsage.UniformBuffer));

            Random rng = new Random(1234);
            Span<byte> rngTmp = stackalloc byte[3];

            TextureRegion[] regions = new TextureRegion[2050];
            for (int i = 1; i < regions.Length; i++)
            {
                rng.NextBytes(rngTmp);
                byte r = rngTmp[0];
                byte g = rngTmp[1];
                byte b = rngTmp[2];
                regions[i] = new TextureRegion(0, r, g, b, 0, 0);

                //new TextureRegion(0, 000, 0, 0, 0, 0),
                //new TextureRegion(0, 032, 0, 0, 0, 0),
                //new TextureRegion(0, 064, 0, 0, 0, 0),
                //new TextureRegion(0, 096, 0, 0, 0, 0),
                //new TextureRegion(0, 128, 0, 0, 0, 0),
                //new TextureRegion(0, 160, 0, 0, 0, 0),
                //new TextureRegion(0, 192, 0, 0, 0, 0),
                //new TextureRegion(0, 224, 0, 0, 0, 0),
                //new TextureRegion(0, 255, 0, 0, 0, 0),
                //new TextureRegion(0, 032, 0, 032, 0, 0),
                //new TextureRegion(0, 064, 0, 064, 0, 0),
                //new TextureRegion(0, 096, 0, 096, 0, 0),
                //new TextureRegion(0, 128, 0, 128, 0, 0),
                //new TextureRegion(0, 160, 0, 160, 0, 0),
                //new TextureRegion(0, 192, 0, 192, 0, 0),
                //new TextureRegion(0, 224, 0, 224, 0, 0),
                //new TextureRegion(0, 255, 0, 255, 0, 0),
            }

            _textureAtlasBuffer = factory.CreateBuffer(new BufferDescription(
                regions.SizeInBytes(), BufferUsage.StructuredBufferReadOnly, (uint)Unsafe.SizeOf<TextureRegion>(), true));
            gd.UpdateBuffer(_textureAtlasBuffer, 0, regions);

            _sharedSet = factory.CreateResourceSet(new ResourceSetDescription(
                sharedLayout,
                _worldInfoBuffer,
                sc.LightInfoBuffer,
                _textureAtlasBuffer));
        }

        public override void DestroyDeviceObjects()
        {
            foreach (ChunkMesh mesh in _meshes)
            {
                mesh.DestroyDeviceObjects();
                ChunkMeshRemoved?.Invoke(mesh);
            }

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.DestroyDeviceObjects();
                    ChunkRegionRemoved?.Invoke(region);
                }
            }

            _disposeCollector.DisposeAll();
        }

        public void Update(in FrameTime time)
        {
            _worldInfo.GlobalTime = time.TotalSeconds;

            ImGuiNET.ImGui.Begin("ChunkRenderer");
            {
                ImGuiNET.ImGui.Text("Triangle Count: " + (_lastTriangleCount / 1000) + "k");
                ImGuiNET.ImGui.Text("Draw Calls: " + _lastDrawCalls);

                lock (_regions)
                {
                    int pending = 0;
                    int count = 0;
                    //foreach (var reg in _regions.Values)
                    //{
                    //    pending += reg.GetPendingChunkCount();
                    //    count += reg.GetChunkCount();
                    //}
                    ImGuiNET.ImGui.Text("Pending Chunks: " + pending);
                    ImGuiNET.ImGui.Text("Chunks: " + count);
                }

                ImGuiNET.ImGui.NewLine();

                ImGuiNET.ImGui.Text(_graphicsBackendName);
                ImGuiNET.ImGui.Text(_graphicsDeviceName);
            }
            ImGuiNET.ImGui.End();
        }

        public void GatherVisibleChunks(List<ChunkMesh> visibleChunks)
        {
            visibleChunks.AddRange(_meshes);
        }

        public void GatherVisibleRegions(List<ChunkMeshRegion> visibleRegions)
        {
            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    visibleRegions.Add(region);
                }
            }
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            for (int i = 0; i < _uploadFences.Length; i++)
            {
                Fence fence = _uploadFences[i];
                if (fence.Signaled)
                {
                    List<ChunkStagingMesh>? submittedMeshes = _uploadSubmittedMeshes[i];
                    foreach (ChunkStagingMesh mesh in submittedMeshes)
                    {
                        _stagingMeshPool.Return(mesh);
                    }
                    submittedMeshes.Clear();

                    gd.ResetFence(fence);
                    _uploadReady[i] = true;
                }
            }

            gd.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

            for (int i = 0; i < _uploadLists.Length; i++)
            {
                if (_uploadReady[i])
                    _uploadLists[i].Begin();
            }

            while (_queuedMeshes.TryDequeue(out ChunkMesh? mesh))
            {
                mesh.CreateDeviceObjects(gd, cl, sc);
                _meshes.Add(mesh);
                ChunkMeshAdded?.Invoke(mesh);
            }

            while (_queuedRegions.TryDequeue(out ChunkMeshRegion? region))
            {
                region.CreateDeviceObjects(gd, cl, sc);
                ChunkRegionAdded?.Invoke(region);
            }

            _lastTriangleCount = 0;
            _lastDrawCalls = 0;

            Camera? renderCamera = RenderCamera;
            if (renderCamera != null)
            {
                ResourceSet renderCameraInfoSet = sc.GetCameraInfoSet(renderCamera);
                RenderMeshes(renderCameraInfoSet, gd, cl, sc);
                RenderRegions(renderCameraInfoSet, gd, cl, sc);
            }

            for (int i = 0; i < _uploadLists.Length; i++)
            {
                if (_uploadReady[i])
                {
                    CommandList uploadList = _uploadLists[i];
                    uploadList.End();

                    gd.SubmitCommands(uploadList, _uploadFences[i]);
                    _uploadReady[i] = false;
                }
            }

            frameEvent.Set();
        }

        private void RenderRegions(ResourceSet cameraInfoSet, GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            List<ChunkMeshRegion> visibleRegions = _visibleRegionBuffer;
            visibleRegions.Clear();
            GatherVisibleRegions(visibleRegions);

            cl.SetPipeline(_indirectPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            bool[] uploadReady = _uploadReady;
            int uploadOffset = 0;
            int maxBuilds = 4;

            for (int i = 0; i < visibleRegions.Count; i++)
            {
                ChunkMeshRegion region = visibleRegions[i];

                if (maxBuilds > 0)
                {
                    if (region.Build(ChunkMesher))
                        maxBuilds--;
                }

                if (region.IsUploadRequired)
                {
                    for (int j = 0; j < uploadReady.Length; j++)
                    {
                        int uploadIndex = (j + uploadOffset) % uploadReady.Length;

                        if (uploadReady[uploadIndex])
                        {
                            ChunkStagingMesh? stagingMesh = region.Upload(gd, _uploadLists[uploadIndex], _stagingMeshPool);
                            if (stagingMesh != null)
                            {
                                _uploadSubmittedMeshes[uploadIndex].Add(stagingMesh);
                                uploadOffset++;
                            }
                            break;
                        }
                    }
                }

                region.Render(gd, cl);

                int triCount = region.IndexCount / 3;
                _lastTriangleCount += triCount;

                if (triCount > 0)
                    _lastDrawCalls++;
            }

            long ss = (long)(ChunkStagingMesh.totalbytesum / (1024.0 * 1024.0 * 20)) * 20;
            if (lastbytesum != ss)
                Console.WriteLine(ss);
            lastbytesum = ss;
        }

        long lastbytesum = 0;

        private void RenderMeshes(ResourceSet cameraInfoSet, GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            List<ChunkMesh> visibleMeshes = _visibleMeshBuffer;
            visibleMeshes.Clear();
            GatherVisibleChunks(visibleMeshes);

            cl.SetPipeline(_directPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            for (int i = 0; i < visibleMeshes.Count; i++)
            {
                ChunkMesh mesh = visibleMeshes[i];
                mesh.Render(gd, cl);

                uint triCount = mesh.TriangleCount;
                _lastTriangleCount += (int)triCount;

                if (triCount > 0)
                    _lastDrawCalls++;
            }
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey(ulong.MaxValue);
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {

        }

        public unsafe void FetchBlockMemory(BlockMemory memory, BlockPosition origin)
        {
            Size3 outerSize = memory.OuterSize;
            Size3 innerSize = memory.InnerSize;
            uint xOffset = (outerSize.W - innerSize.W) / 2;
            uint yOffset = (outerSize.H - innerSize.H) / 2;
            uint zOffset = (outerSize.D - innerSize.D) / 2;

            BlockPosition blockOffset = new(
                origin.X - (int)xOffset,
                origin.Y - (int)yOffset,
                origin.Z - (int)zOffset);
            WorldBox fetchBox = new(blockOffset, outerSize);

            foreach (ChunkBox chunkBox in fetchBox.EnumerateChunkBoxes())
            {
                nint outerOriginX = chunkBox.OuterOrigin.X;
                nint outerOriginY = chunkBox.OuterOrigin.Y;
                nint outerOriginZ = chunkBox.OuterOrigin.Z;
                nint outerSizeD = (nint)outerSize.D;
                nint outerSizeW = (nint)outerSize.W;
                nint innerSizeH = (nint)chunkBox.Size.H;
                nint innerSizeD = (nint)chunkBox.Size.D;
                uint innerSizeW = chunkBox.Size.W;

                if (_chunks.TryGetValue(chunkBox.Chunk, out Chunk? chunk))
                {
                    nint innerOriginX = chunkBox.InnerOrigin.X;
                    nint innerOriginY = chunkBox.InnerOrigin.Y;
                    nint innerOriginZ = chunkBox.InnerOrigin.Z;

                    for (nint y = 0; y < innerSizeH; y++)
                    {
                        for (nint z = 0; z < innerSizeD; z++)
                        {
                            nint outerBaseIndex = BlockMemory.GetIndexBase(
                                outerSizeD,
                                outerSizeW,
                                y + outerOriginY,
                                z + outerOriginZ)
                                + outerOriginX;

                            ref uint destination = ref memory.Data[outerBaseIndex];

                            chunk.GetBlockRowUnsafe(
                                innerOriginX,
                                y + innerOriginY,
                                z + innerOriginZ,
                                ref destination,
                                innerSizeW);
                        }
                    }
                }
                else
                {
                    for (nint y = 0; y < innerSizeH; y++)
                    {
                        for (nint z = 0; z < innerSizeD; z++)
                        {
                            nint outerBaseIndex = BlockMemory.GetIndexBase(
                                outerSizeD,
                                outerSizeW,
                                y + outerOriginY,
                                z + outerOriginZ)
                                + outerOriginX;

                            Unsafe.InitBlockUnaligned(
                                ref Unsafe.As<uint, byte>(ref memory.Data[outerBaseIndex]),
                                0,
                                innerSizeW * sizeof(uint));
                        }
                    }
                }
            }
        }

        public BlockMemory FetchBlockMemory(BlockPosition origin, Size3 innerSize, Size3 outerSize)
        {
            BlockMemory memory = new(innerSize, outerSize);
            FetchBlockMemory(memory, origin);
            return memory;
        }

        public Size3 GetBlockMemoryInnerSize()
        {
            return Chunk.Size;
        }

        public Size3 GetBlockMemoryOuterSize()
        {
            const int doubleMargin = FetchBlockMargin * 2;

            Size3 innerSize = GetBlockMemoryInnerSize();

            return new Size3(
                innerSize.W + doubleMargin,
                innerSize.H + doubleMargin,
                innerSize.D + doubleMargin);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldInfo
    {
        public float GlobalTime;

        private float _padding0;
        private float _padding1;
        private float _padding2;
    }
}
