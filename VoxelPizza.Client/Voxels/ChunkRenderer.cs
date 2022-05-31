using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;
using VoxelPizza.Collections;
using VoxelPizza.Diagnostics;
using VoxelPizza.Memory;
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

        private ChunkGraph _graph = new();

        private List<ChunkMeshRegion> _visibleRegionBuffer = new();
        private int[] _listSortBuffer = Array.Empty<int>();
        private Dictionary<RenderRegionPosition, ChunkMeshRegion> _regions = new();
        private ConcurrentQueue<ChunkMeshRegion> _queuedRegions = new();
        private Stack<ChunkMeshRegion> _regionPool = new();

        private BlockMemory _blockBuffer;

        private WorldInfo _worldInfo;

        private uint _lastTriangleCount;
        private uint _lastDrawCalls;

        public Dimension Dimension { get; }
        public MemoryHeap ChunkMeshHeap { get; }
        public Size3 RegionSize { get; }
        public ChunkMesher ChunkMesher { get; }

        public ResourceLayout ChunkSharedLayout { get; private set; }
        public ResourceLayout ChunkInfoLayout { get; private set; }

        public Camera? RenderCamera { get; set; }
        public Camera? CullCamera { get; set; }

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public event Action<ChunkMeshRegion>? RenderRegionAdded;
        public event Action<ChunkMeshRegion>? RenderRegionRemoved;

        public ChunkRenderer(Dimension dimension, MemoryHeap chunkMeshHeap, Size3 regionSize)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            ChunkMeshHeap = chunkMeshHeap ?? throw new ArgumentNullException(nameof(chunkMeshHeap));
            RegionSize = regionSize;
            ChunkMesher = new ChunkMesher(ChunkMeshHeap);

            _stagingMeshPool = new ChunkStagingMeshPool(16);
            _commandListFencePool = new CommandListFencePool(16 + 2);
            _workers = new ChunkRendererWorker[2];

            for (int i = 0; i < _workers.Length; i++)
            {
                BlockMemory blockMemory = new(
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

            _graph.SidesFulfilled += ChunkGraph_SidesFulfilled;

            _blockBuffer = new BlockMemory(
                GetBlockMemoryInnerSize(),
                GetBlockMemoryOuterSize());
        }

        public void ReuploadRegions()
        {
            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.UploadRequired();
                }
            }
        }

        public void RebuildChunks()
        {
            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    Size3 size = region.Size;
                    ChunkPosition regPos = region.Position.ToChunk(RegionSize);

                    for (int y = 0; y < size.H; y++)
                    {
                        for (int z = 0; z < size.D; z++)
                        {
                            for (int x = 0; x < size.W; x++)
                            {
                                region.RequestBuild(new ChunkPosition(x + regPos.X, y + regPos.Y, z + regPos.Z));
                            }
                        }
                    }
                }
            }
        }

        private void ChunkGraph_SidesFulfilled(ChunkRegionGraph regionGraph, ChunkPosition localChunkPosition)
        {
            RequestBuild(regionGraph.RegionPosition.OffsetLocalChunk(localChunkPosition));
        }

        public ChunkMeshRegion RentMeshRegion(RenderRegionPosition position)
        {
            ChunkMeshRegion? renderRegion;
            lock (_regionPool)
            {
                if (!_regionPool.TryPop(out renderRegion))
                {
                    renderRegion = new ChunkMeshRegion(this, RegionSize);
                }
            }
            renderRegion.SetPosition(position);
            return renderRegion;
        }

        public void RecycleMeshRegion(ChunkMeshRegion meshRegion)
        {
            lock (_regionPool)
            {
                _regionPool.Push(meshRegion);
            }
        }

        private ChunkMeshRegion GetOrCreateRenderRegion(RenderRegionPosition regionPosition)
        {
            lock (_regions)
            {
                if (!_regions.TryGetValue(regionPosition, out ChunkMeshRegion? renderRegion))
                {
                    renderRegion = RentMeshRegion(regionPosition);

                    _regions.Add(regionPosition, renderRegion);
                    RenderRegionAdded?.Invoke(renderRegion);

                    _queuedRegions.Enqueue(renderRegion);
                }
                return renderRegion;
            }
        }

        private void Dimension_ChunkAdded(Chunk chunk)
        {
            RenderRegionPosition regionPosition = GetRegionPosition(chunk.Position);
            ChunkMeshRegion renderRegion = GetOrCreateRenderRegion(regionPosition);

            renderRegion.ChunkAdded(chunk.Position);

            lock (_graph)
            {
                _graph.AddChunk(chunk.Position, chunk.IsEmpty);
            }
        }

        private void Dimension_ChunkRemoved(Chunk chunk)
        {
            RenderRegionPosition regionPosition = GetRegionPosition(chunk.Position);
            if (TryGetRegion(regionPosition, out ChunkMeshRegion? renderRegion))
            {
                renderRegion.ChunkRemoved(chunk.Position);

                if (renderRegion.ChunkCount == 0)
                {
                    lock (_regions)
                    {
                        _regions.Remove(regionPosition);
                    }
                    RenderRegionRemoved?.Invoke(renderRegion);

                    renderRegion.DestroyDeviceObjects();

                    //for (int i = 0; i < _workers.Length; i++)
                    {
                        _workers[0].EnqueueReset(renderRegion);
                    }
                }
                else
                {
                    renderRegion.RequestBuild(chunk.Position);
                }
            }

            lock (_graph)
            {
                _graph.RemoveChunk(chunk.Position);
            }
        }

        private void Dimension_ChunkUpdated(Chunk chunk)
        {
            ChunkGraphFaces faces;
            lock (_graph)
            {
                faces = _graph.GetChunk(chunk.Position);
                if ((faces & ChunkGraphFaces.Empty) == ChunkGraphFaces.Empty)
                {
                    if (chunk.IsEmpty)
                    {
                        return;
                    }
                    _graph.RemoveChunkEmptyFlag(chunk.Position);
                }
            }

            if ((faces & ChunkGraphFaces.AllSides) == ChunkGraphFaces.AllSides)
            {
                RequestBuild(chunk.Position);
            }
        }

        private bool TryGetRegion(RenderRegionPosition regionPosition, [MaybeNullWhen(false)] out ChunkMeshRegion renderRegion)
        {
            lock (_regions)
            {
                return _regions.TryGetValue(regionPosition, out renderRegion);
            }
        }

        private void RequestBuild(ChunkPosition chunkPosition)
        {
            RenderRegionPosition regionPosition = GetRegionPosition(chunkPosition);
            if (TryGetRegion(regionPosition, out ChunkMeshRegion? region))
            {
                region.RequestBuild(chunkPosition);
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

            VertexLayoutDescription spaceLayout = new(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription paintLayout = new(
                new VertexElementDescription("TexAnimation0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),
                new VertexElementDescription("TexRegion0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1));

            VertexLayoutDescription worldLayout = new(
                new VertexElementDescription("Translation", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4))
            {
                InstanceStepRate = 1
            };

            (Shader mainVs, Shader mainFs, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkMain");

            RasterizerStateDescription rasterizerState = RasterizerStateDescription.Default;
            //rasterizerState.CullMode = FaceCullMode.None;
            //rasterizerState.FillMode = PolygonFillMode.Wireframe;

            DepthStencilStateDescription depthStencilState = gd.IsDepthRangeZeroToOne
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

            Random rng = new(1234);
            Span<byte> rngTmp = stackalloc byte[3];

            TextureRegion[] regions = new TextureRegion[2050];
            for (int i = 1; i < regions.Length; i++)
            {
                rng.NextBytes(rngTmp);
                byte r = rngTmp[0];
                byte g = rngTmp[1];
                byte b = rngTmp[2];
                regions[i] = new TextureRegion(0, r, g, b, 0, 0);
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

        public void Update(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            _worldInfo.GlobalTime = state.Time.TotalSeconds;

            ImGuiNET.ImGui.Begin("ChunkRenderer");
            {
                ImGuiNET.ImGui.Text("Block: " + Dimension.PlayerChunkPosition.ToBlock().ToString());
                ImGuiNET.ImGui.Text("Chunk: " + Dimension.PlayerChunkPosition.ToString());
                ImGuiNET.ImGui.Text("Region: " + new RenderRegionPosition(Dimension.PlayerChunkPosition, RegionSize).ToString());

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

        private static int ManhattanDistance(BlockPosition a, BlockPosition b)
        {
            BlockPosition d = BlockPosition.Abs(a - b);
            return d.X + d.Y + d.Z;
        }

        public void GatherVisibleRegions(
            SceneContext sc,
            BoundingFrustum4? cullFrustum,
            List<ChunkMeshRegion> visibleRegions)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            lock (_regions)
            {
                if (cullFrustum.HasValue)
                {
                    BoundingFrustum4 frustum = cullFrustum.GetValueOrDefault();

                    foreach (ChunkMeshRegion region in _regions.Values)
                    {
                        Debug.Assert(region.ChunkCount >= 0);

                        if (region.ChunkCount == 0)
                        {
                            continue;
                        }

                        Vector4 regionPos = region.Position.ToBlock(region.Size);
                        BoundingBox4 box = new(regionPos, regionPos + (region.Size * Chunk.Size));

                        if (frustum.Contains(box) != ContainmentType.Disjoint)
                        {
                            visibleRegions.Add(region);
                            continue;
                        }
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
        }

        public void SortVisibleRegions(SceneContext sc, Vector3 cullOrigin, List<ChunkMeshRegion> visibleRegions)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            BlockPosition blockCullOrigin = new(
                (int)MathF.Round(cullOrigin.X),
                (int)MathF.Round(cullOrigin.Y),
                (int)MathF.Round(cullOrigin.Z));

            Span<ChunkMeshRegion> regions = CollectionsMarshal.AsSpan(visibleRegions);

            if (_listSortBuffer.Length < regions.Length)
            {
                Array.Resize(ref _listSortBuffer, regions.Length + 4096);
            }
            Span<int> distances = _listSortBuffer.AsSpan(0, regions.Length);

            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = ManhattanDistance(regions[i].Position.ToBlock(RegionSize), blockCullOrigin);
            }
            distances.Sort(regions);
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            cl.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

            while (_queuedRegions.TryDequeue(out ChunkMeshRegion? region))
            {
                region.CreateDeviceObjects(gd, cl, sc);
            }

            _lastTriangleCount = 0;
            _lastDrawCalls = 0;

            Camera? renderCamera = RenderCamera;
            if (renderCamera != null)
            {
                ResourceSet renderCameraInfoSet = sc.GetCameraInfoSet(renderCamera);

                Vector3? cullOrigin = null;
                BoundingFrustum4? cullFrustum = null;

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
            ResourceSet cameraInfoSet, Vector3? cullOrigin, BoundingFrustum4? cullFrustum)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            List<ChunkMeshRegion> visibleRegions = _visibleRegionBuffer;
            visibleRegions.Clear();
            GatherVisibleRegions(sc, cullFrustum, visibleRegions);

            if (cullOrigin.HasValue)
            {
                SortVisibleRegions(sc, cullOrigin.Value, visibleRegions);
            }

            cl.SetPipeline(_indirectPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            Render(gd, cl, sc, visibleRegions);
        }

        public static List<List<T>> SplitList<T>(List<T> elements, int splitCount)
        {
            List<List<T>> list = new();

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

        private void Render(
            GraphicsDevice gd, CommandList cl, SceneContext sc,
            List<ChunkMeshRegion> meshesToBuild)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            List<List<ChunkMeshRegion>> buildSplits = SplitList(meshesToBuild, _workers.Length);
            for (int i = 0; i < buildSplits.Count; i++)
            {
                _workers[i].SignalToBuild(buildSplits[i].GetEnumerator());
            }

            foreach (ChunkMeshRegion mesh in meshesToBuild)
            {
                mesh.Render(cl);

                uint triCount = mesh.IndexCount / 3;
                _lastTriangleCount += triCount;

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

        public static BlockMemoryState FetchBlockMemory(
            Dimension dimension, BlockMemory blockBuffer, BlockPosition origin)
        {
            ref uint data = ref MemoryMarshal.GetArrayDataReference(blockBuffer.Data);
            Size3 outerSize = blockBuffer.OuterSize;
            Size3 innerSize = blockBuffer.InnerSize;
            uint xOffset = (outerSize.W - innerSize.W) / 2;
            uint yOffset = (outerSize.H - innerSize.H) / 2;
            uint zOffset = (outerSize.D - innerSize.D) / 2;

            BlockPosition blockOffset = new(
                origin.X - (int)xOffset,
                origin.Y - (int)yOffset,
                origin.Z - (int)zOffset);
            WorldBox fetchBox = new(blockOffset, outerSize);

            ChunkBoxSliceEnumerator chunkBoxEnumerator = fetchBox.EnumerateChunkBoxSlices();
            int maxChunkCount = chunkBoxEnumerator.GetMaxChunkCount();

            Span<ChunkBoxSlice> chunkBoxes = blockBuffer.GetChunkBoxBuffer(maxChunkCount);
            Span<bool> emptyChunks = blockBuffer.GetEmptyChunkBuffer(maxChunkCount);

            int chunkCount = 0;
            int emptyCount = 0;

            foreach (ChunkBoxSlice chunkBox in chunkBoxEnumerator)
            {
                chunkBoxes[chunkCount++] = chunkBox;
            }

            chunkBoxes = chunkBoxes[..chunkCount];
            emptyChunks = emptyChunks[..chunkCount];

            for (int i = 0; i < chunkCount; i++)
            {
                ref ChunkBoxSlice chunkBox = ref chunkBoxes[i];
                using RefCounted<Chunk?> countedChunk = dimension.GetChunk(chunkBox.Chunk);

                if (!countedChunk.TryGetValue(out Chunk? chunk) || chunk.IsEmpty)
                {
                    emptyCount++;
                    emptyChunks[i] = true;
                    continue;
                }
                emptyChunks[i] = false;

                nuint outerOriginX = (nuint)chunkBox.OuterOrigin.X;
                nuint outerOriginY = (nuint)chunkBox.OuterOrigin.Y;
                nuint outerOriginZ = (nuint)chunkBox.OuterOrigin.Z;
                nuint outerSizeD = outerSize.D;
                nuint outerSizeW = outerSize.W;
                nuint innerSizeH = chunkBox.Size.H;
                nuint innerSizeD = chunkBox.Size.D;
                nuint innerSizeW = chunkBox.Size.W;

                nuint innerOriginX = (nuint)chunkBox.InnerOrigin.X;
                nuint innerOriginY = (nuint)chunkBox.InnerOrigin.Y;
                nuint innerOriginZ = (nuint)chunkBox.InnerOrigin.Z;

                BlockStorage storage = chunk.GetBlockStorage();

                for (nuint y = 0; y < innerSizeH; y++)
                {
                    for (nuint z = 0; z < innerSizeD; z++)
                    {
                        nuint outerBaseIndex = BlockMemory.GetIndexBase(
                            outerSizeD,
                            outerSizeW,
                            y + outerOriginY,
                            z + outerOriginZ)
                            + outerOriginX;

                        ref uint destination = ref Unsafe.Add(ref data, outerBaseIndex);

                        storage.GetBlockRow(
                            innerOriginX,
                            y + innerOriginY,
                            z + innerOriginZ,
                            ref destination,
                            innerSizeW);
                    }
                }
            }

            if (emptyCount == chunkCount)
            {
                return BlockMemoryState.Uninitialized;
            }

            if (emptyCount == 0)
            {
                return BlockMemoryState.Filled;
            }

            for (int i = 0; i < chunkCount; i++)
            {
                if (!emptyChunks[i])
                {
                    continue;
                }

                ref ChunkBoxSlice chunkBox = ref chunkBoxes[i];
                nuint outerOriginX = (nuint)chunkBox.OuterOrigin.X;
                nuint outerOriginY = (nuint)chunkBox.OuterOrigin.Y;
                nuint outerOriginZ = (nuint)chunkBox.OuterOrigin.Z;
                nuint outerSizeD = outerSize.D;
                nuint outerSizeW = outerSize.W;
                nuint innerSizeH = chunkBox.Size.H;
                nuint innerSizeD = chunkBox.Size.D;
                uint innerSizeW = chunkBox.Size.W;

                for (nuint y = 0; y < innerSizeH; y++)
                {
                    for (nuint z = 0; z < innerSizeD; z++)
                    {
                        nuint outerBaseIndex = BlockMemory.GetIndexBase(
                            outerSizeD,
                            outerSizeW,
                            y + outerOriginY,
                            z + outerOriginZ)
                            + outerOriginX;

                        Unsafe.InitBlockUnaligned(
                            ref Unsafe.As<uint, byte>(ref blockBuffer.Data[outerBaseIndex]),
                            0,
                            innerSizeW * sizeof(uint));
                    }
                }
            }
            return BlockMemoryState.Filled;
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
