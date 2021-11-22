using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    // TODO: separate commonly updated chunks into singular mesh instances

    public class ChunkRenderer : Renderable, IUpdateable
    {
        public ChunkGraph graph = new ChunkGraph();

        /// <summary>
        /// The amount of blocks that are fetched around a chunk for meshing.
        /// </summary>
        public const int FetchBlockMargin = 2;

        private string _graphicsDeviceName;
        private string _graphicsBackendName;

        private ChunkStagingMeshPool _stagingMeshPool;
        private CommandListFencePool _commandListFencePool;
        private ChunkRendererWorker[] _workers;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;

        private Pipeline _directPipeline;
        private Pipeline _indirectPipeline;
        private ResourceSet _sharedSet;

        private List<ChunkMeshRegion> _visibleRegionBuffer = new();
        private List<ChunkMeshRegion> _cleanupRegionBuffer = new();
        private Dictionary<RenderRegionPosition, ChunkMeshRegion> _regions = new();
        private ConcurrentQueue<ChunkMeshRegion> _queuedRegions = new();

        // TODO: hide this
        public ConcurrentStack<ChunkMeshRegion> _regionPool = new();

        private WorldInfo _worldInfo;

        private uint _lastTriangleCount;
        private uint _lastDrawCalls;

        public Dimension Dimension { get; }
        public HeapPool ChunkMeshPool { get; }
        public Size3 RegionSize { get; }
        public ChunkMesher ChunkMesher { get; }

        public ResourceLayout ChunkSharedLayout { get; private set; }
        public ResourceLayout ChunkInfoLayout { get; private set; }

        public Camera? RenderCamera { get; set; }
        public Camera? CullCamera { get; set; }

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public event Action<ChunkMeshRegion>? RenderRegionAdded;
        public event Action<ChunkMeshRegion>? RenderRegionRemoved;

        public ChunkRenderer(Dimension dimension, HeapPool chunkMeshPool, Size3 regionSize)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            ChunkMeshPool = chunkMeshPool ?? throw new ArgumentNullException(nameof(chunkMeshPool));
            RegionSize = regionSize;
            ChunkMesher = new ChunkMesher(ChunkMeshPool);

            _stagingMeshPool = new ChunkStagingMeshPool(4);
            _commandListFencePool = new CommandListFencePool(6);
            _workers = new ChunkRendererWorker[2];

            for (int i = 0; i < _workers.Length; i++)
            {
                var blockMemory = new BlockMemory(
                    GetBlockMemoryInnerSize(),
                    GetBlockMemoryOuterSize());

                _workers[i] = new ChunkRendererWorker(ChunkMesher, blockMemory, _stagingMeshPool, _commandListFencePool)
                {
                    WorkerName = $"Chunk Renderer Worker {i + 1}"
                };
            }

            dimension.ChunkAdded += Dimension_ChunkAdded;
            dimension.ChunkUpdated += Dimension_ChunkUpdated;
            dimension.ChunkRemoved += Dimension_ChunkRemoved;

            graph.AddedAllSides += Graph_AddedAllSides;
        }

        public void ReuploadAll()
        {
            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.RequestBuild(new ChunkPosition(0, 0, 0));
                }
            }
        }

        private void Graph_AddedAllSides(ChunkRegionGraph regionGraph, ChunkPosition localChunkPosition)
        {
            RequestBuild(regionGraph.RegionPosition.OffsetLocalChunk(localChunkPosition));
        }

        private void Dimension_ChunkAdded(Chunk chunk)
        {
            RenderRegionPosition regionPosition = GetRegionPosition(chunk.Position);
            lock (_regions)
            {
                if (!_regions.TryGetValue(regionPosition, out ChunkMeshRegion? renderRegion))
                {
                    if (!_regionPool.TryPop(out renderRegion))
                    {
                        renderRegion = new ChunkMeshRegion(this, RegionSize);
                    }

                    renderRegion.SetPosition(regionPosition);

                    _regions.Add(regionPosition, renderRegion);

                    _queuedRegions.Enqueue(renderRegion);
                }

                renderRegion.ChunkAdded(chunk.Position);
            }

            lock (graph)
            {
                graph.AddChunk(chunk.Position);
            }
        }

        private void Dimension_ChunkRemoved(Chunk chunk)
        {
            lock (graph)
            {
                graph.RemoveChunk(chunk.Position);
            }

            RenderRegionPosition regionPosition = GetRegionPosition(chunk.Position);
            lock (_regions)
            {
                if (_regions.TryGetValue(regionPosition, out ChunkMeshRegion? renderRegion))
                {
                    renderRegion.ChunkRemoved(chunk.Position);

                    renderRegion.RequestRemove(chunk.Position);

                    if (renderRegion.ChunkCount == 0)
                    {
                        _regions.Remove(regionPosition);
                        RenderRegionRemoved?.Invoke(renderRegion);

                        //for (int i = 0; i < _workers.Length; i++)
                        {
                            _workers[0].EnqueueReset(renderRegion);
                        }
                    }
                }
            }
        }

        private void Dimension_ChunkUpdated(Chunk chunk)
        {
            RequestBuild(chunk.Position);
        }

        private void RequestBuild(ChunkPosition chunkPosition)
        {
            RenderRegionPosition regionPosition = GetRegionPosition(chunkPosition);
            lock (_regions)
            {
                if (_regions.TryGetValue(regionPosition, out ChunkMeshRegion? meshRegion))
                {
                    lock (graph)
                    {
                        ChunkGraphFaces faces = graph.GetChunk(chunkPosition);
                        //if ((faces & ChunkGraphFaces.AllSides) == ChunkGraphFaces.AllSides)
                        {
                            meshRegion.RequestBuild(chunkPosition);
                        }
                    }
                }
            }
        }

        public RenderRegionPosition GetRegionPosition(ChunkPosition chunkPosition)
        {
            return new RenderRegionPosition(
                IntMath.DivideRoundDown(chunkPosition.X, (int)RegionSize.W),
                IntMath.DivideRoundDown(chunkPosition.Y, (int)RegionSize.H),
                IntMath.DivideRoundDown(chunkPosition.Z, (int)RegionSize.D));
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _graphicsDeviceName = gd.DeviceName;
            _graphicsBackendName = gd.BackendType.ToString();

            ResourceFactory factory = gd.ResourceFactory;

            _stagingMeshPool.CreateDeviceObjects(gd, cl, sc);
            _commandListFencePool.CreateDeviceObjects(gd, cl, sc);

            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i].CreateDeviceObjects(gd, cl, sc);
            }

            ChunkSharedLayout = factory.CreateResourceLayout(
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
                new VertexElementDescription("Translation", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4))
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
                new[] { sc.CameraInfoLayout, ChunkSharedLayout, ChunkInfoLayout },
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
                new[] { sc.CameraInfoLayout, ChunkSharedLayout },
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
                ChunkSharedLayout,
                _worldInfoBuffer,
                sc.LightInfoBuffer,
                _textureAtlasBuffer));

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.CreateDeviceObjects(gd, cl, sc);
                }
            }
        }

        public override void DestroyDeviceObjects()
        {
            foreach (ChunkRendererWorker worker in _workers)
            {
                worker.DestroyDeviceObjects();
            }

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.DestroyDeviceObjects();
                }
            }

            _stagingMeshPool.DestroyDeviceObjects();
            _commandListFencePool.DestroyDeviceObjects();

            ChunkSharedLayout.Dispose();
            ChunkInfoLayout.Dispose();
            _directPipeline.Dispose();
            _indirectPipeline.Dispose();
            _worldInfoBuffer.Dispose();
            _textureAtlasBuffer.Dispose();
            _sharedSet.Dispose();
        }

        public void Update(in FrameTime time)
        {
            _worldInfo.GlobalTime = time.TotalSeconds;

            if (CullCamera != null)
            {
                // TODO: remove this

                Vector3 cullCameraPos = CullCamera.Position;
                Dimension.PlayerChunkPosition = new BlockPosition(
                    (int)MathF.Round(cullCameraPos.X),
                    (int)MathF.Round(cullCameraPos.Y),
                    (int)MathF.Round(cullCameraPos.Z)).ToChunk();
            }

            ImGuiNET.ImGui.Begin("ChunkRenderer");
            {
                ImGuiNET.ImGui.Text("Triangle Count: " + (_lastTriangleCount / 1000) + "k");
                ImGuiNET.ImGui.Text("Draw Calls: " + _lastDrawCalls);

                int chunkCount = 0;
                int toBuild = 0;
                int toUpload = 0;
                int toRemove = 0;
                int regionCount = 0;

                lock (_regions)
                {
                    foreach (ChunkMeshRegion reg in _regions.Values)
                    {
                        (int ChunkCount, int ToBuild, int ToUpload, int ToRemove) = reg.GetCounts();
                        chunkCount += ChunkCount;
                        toBuild += ToBuild;
                        toUpload += ToUpload;
                        toRemove += ToRemove;
                    }
                    regionCount = _regions.Count;
                }

                ImGuiNET.ImGui.Text("Chunks to build: " + toBuild);
                ImGuiNET.ImGui.Text("Chunks to upload: " + toUpload);
                ImGuiNET.ImGui.Text("Chunks to remove: " + toRemove);
                ImGuiNET.ImGui.Text("Chunks: " + chunkCount);
                ImGuiNET.ImGui.Text("Render regions: " + regionCount);

                ImGuiNET.ImGui.NewLine();

                ImGuiNET.ImGui.Text(_graphicsBackendName);
                ImGuiNET.ImGui.Text(_graphicsDeviceName);
            }
            ImGuiNET.ImGui.End();
        }

        private static float ManhattanDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = Vector3.Abs(a - b);
            return d.X + d.Y + d.Z;
        }

        public void GatherVisibleRegions(
            SceneContext sc,
            Vector3? cullOrigin,
            BoundingFrustum? cullFrustum,
            List<ChunkMeshRegion> visibleRegions,
            List<ChunkMeshRegion> cleanupRegions)
        {
            using var profilerToken = sc.Profiler.Push();

            Vector3 origin = cullOrigin.GetValueOrDefault();

            lock (_regions)
            {
                if (cullFrustum.HasValue)
                {
                    BoundingFrustum frustum = cullFrustum.GetValueOrDefault();
                    int c = 0;
                    foreach (ChunkMeshRegion region in _regions.Values)
                    {
                        Debug.Assert(region.ChunkCount >= 0);

                        if (region.ChunkCount == 0)
                        {
                            c++;
                            continue;
                        }

                        Vector4 regionPos = region.Position.ToBlock(region.Size);
                        BoundingBox box = new(regionPos, regionPos + (region.Size * Chunk.Size));

                        if (frustum.Contains(box) != ContainmentType.Disjoint)
                        {
                            visibleRegions.Add(region);
                            continue;
                        }

                        //if (region.IsRemoveRequired)
                        //{
                        //    cleanupRegions.Add(region);
                        //}
                    }
                }
                else
                {
                    foreach (ChunkMeshRegion region in _regions.Values)
                    {
                        visibleRegions.Add(region);
                    }
                }
            }

            if (cullOrigin.HasValue)
            {
                visibleRegions.Sort((x, y) =>
                {
                    float a = ManhattanDistance(x.Position.ToBlock(RegionSize), origin);
                    float b = ManhattanDistance(y.Position.ToBlock(RegionSize), origin);
                    return a.CompareTo(b);
                });
            }
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            using var profilerToken = sc.Profiler.Push();

            cl.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

            while (_queuedRegions.TryDequeue(out ChunkMeshRegion? region))
            {
                region.CreateDeviceObjects(gd, cl, sc);
                RenderRegionAdded?.Invoke(region);
            }

            _lastTriangleCount = 0;
            _lastDrawCalls = 0;

            Camera? renderCamera = RenderCamera;
            if (renderCamera != null)
            {
                ResourceSet renderCameraInfoSet = sc.GetCameraInfoSet(renderCamera);

                Vector3? cullOrigin = null;
                BoundingFrustum? cullFrustum = null;

                Camera? cullCamera = CullCamera;
                if (cullCamera != null)
                {
                    cullOrigin = cullCamera.Position;
                    cullFrustum = new(cullCamera.ViewMatrix * cullCamera.ProjectionMatrix);
                }

                RenderRegions(gd, cl, sc, renderCameraInfoSet, cullOrigin, cullFrustum);
            }
        }

        private void RenderRegions(
            GraphicsDevice gd, CommandList cl, SceneContext sc,
            ResourceSet cameraInfoSet, Vector3? cullOrigin, BoundingFrustum? cullFrustum)
        {
            using var profilerToken = sc.Profiler.Push();

            List<ChunkMeshRegion> visibleRegions = _visibleRegionBuffer;
            List<ChunkMeshRegion> cleanupRegions = _cleanupRegionBuffer;
            visibleRegions.Clear();
            cleanupRegions.Clear();
            GatherVisibleRegions(sc, cullOrigin, cullFrustum, visibleRegions, cleanupRegions);

            cl.SetPipeline(_indirectPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            Render(gd, cl, sc, visibleRegions, cleanupRegions);
        }

        public static List<List<T>> SplitList<T>(List<T> elements, int splitCount)
        {
            var list = new List<List<T>>();

            int elementsPerList = elements.Count / splitCount;

            if (elements.Count > splitCount)
            {
                for (int i = 0; i < splitCount; i++)
                {
                    list.Add(elements.GetRange(i * elementsPerList, Math.Min(elementsPerList, elements.Count - i)));
                }
            }

            if (list.Count > 0)
                list[^1].AddRange(elements.GetRange(splitCount * elementsPerList, elements.Count - splitCount * elementsPerList));

            return list;
        }

        long lastbytesum = 0;

        private void Render(
            GraphicsDevice gd, CommandList cl, SceneContext sc,
            List<ChunkMeshRegion> meshesToBuild,
            List<ChunkMeshRegion> meshesToCleanup)
        {
            using var profilerToken = sc.Profiler.Push();

            List<List<ChunkMeshRegion>> buildSplits = SplitList(meshesToBuild, _workers.Length);
            for (int i = 0; i < buildSplits.Count; i++)
            {
                _workers[i].SignalToBuild(buildSplits[i].GetEnumerator());
            }

            List<List<ChunkMeshRegion>> cleanupSplits = SplitList(meshesToCleanup, _workers.Length);
            for (int i = 0; i < cleanupSplits.Count; i++)
            {
                _workers[i].SignalToCleanup(cleanupSplits[i].GetEnumerator());
            }

            foreach (ChunkMeshRegion mesh in meshesToBuild)
            {
                mesh.Render(cl);

                uint triCount = mesh.IndexCount / 3;
                _lastTriangleCount += triCount;

                if (triCount > 0)
                    _lastDrawCalls++;
            }

            long ss = (long)(ChunkStagingMesh.totalbytesum / (1024.0 * 1024.0 * 20)) * 20;
            if (lastbytesum != ss)
                Console.WriteLine(ss);
            lastbytesum = ss;
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
            ref uint data = ref MemoryMarshal.GetArrayDataReference(memory.Data);
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

                Chunk? chunk = Dimension.GetChunk(chunkBox.Chunk);
                if (chunk != null)
                {
                    try
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

                                ref uint destination = ref Unsafe.Add(ref data, outerBaseIndex);

                                chunk.GetBlockRow(
                                    innerOriginX,
                                    y + innerOriginY,
                                    z + innerOriginZ,
                                    ref destination,
                                    innerSizeW);
                            }
                        }
                    }
                    finally
                    {
                        chunk.DecrementRef();
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (ChunkRendererWorker worker in _workers)
            {
                worker.Dispose();
            }

            _stagingMeshPool.Dispose();
            _commandListFencePool.Dispose();
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
