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
using VoxelPizza.Diagnostics;
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

        private int _regionBuildLimit = 8;
        private int _meshBuildLimit = 32;

        private bool[] _uploadReady;
        private CommandList[] _uploadLists;
        private Fence[] _uploadFences;
        private ChunkStagingMeshPool _stagingMeshPool;
        private List<ChunkStagingMesh>[] _uploadSubmittedMeshes;
        private BlockMemory _blockMemory;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;

        private Pipeline _directPipeline;
        private Pipeline _indirectPipeline;
        private ResourceSet _sharedSet;

        private AutoResetEvent frameEvent = new(false);

        private List<ChunkMesh> _visibleMeshBuffer = new();
        private List<ChunkMesh> _meshes = new();
        private ConcurrentQueue<ChunkMesh> _queuedMeshes = new();

        private List<ChunkMeshRegion> _visibleRegionBuffer = new();
        private Dictionary<RenderRegionPosition, ChunkMeshRegion> _regions = new();
        private ConcurrentQueue<ChunkMeshRegion> _queuedRegions = new();

        private WorldInfo _worldInfo;

        private int _lastTriangleCount;
        private int _lastDrawCalls;

        public Dimension Dimension { get; }
        public HeapPool ChunkMeshPool { get; }
        public Size3 RegionSize { get; }
        public ChunkMesher ChunkMesher { get; }

        public ResourceLayout ChunkSharedLayout { get; private set; }
        public ResourceLayout ChunkInfoLayout { get; private set; }

        public Camera? RenderCamera { get; set; }
        public Camera? CullCamera { get; set; }

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public event Action<ChunkMesh>? ChunkMeshAdded;
        public event Action<ChunkMeshRegion>? RenderRegionAdded;

        public event Action<ChunkMesh>? ChunkMeshRemoved;
        public event Action<ChunkMeshRegion>? RenderRegionRemoved;

        public ChunkRenderer(Dimension dimension, HeapPool chunkMeshPool, Size3 regionSize)
        {
            Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
            ChunkMeshPool = chunkMeshPool ?? throw new ArgumentNullException(nameof(chunkMeshPool));
            RegionSize = regionSize;
            ChunkMesher = new ChunkMesher(ChunkMeshPool);

            _uploadReady = new bool[32];
            _uploadLists = new CommandList[_uploadReady.Length];
            _uploadFences = new Fence[_uploadReady.Length];
            _uploadSubmittedMeshes = new List<ChunkStagingMesh>[_uploadReady.Length];

            _blockMemory = new BlockMemory(
                GetBlockMemoryInnerSize(),
                GetBlockMemoryOuterSize());

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadSubmittedMeshes[i] = new List<ChunkStagingMesh>();
            }

            dimension.ChunkAdded += Dimension_ChunkAdded;
        }

        private void Dimension_ChunkAdded(Chunk chunk)
        {
            var mesh = new ChunkMesh(this, chunk.Position);
            _queuedMeshes.Enqueue(mesh);

            mesh.RequestBuild(chunk.Position);
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

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadReady[i] = true;
                _uploadLists[i] = factory.CreateCommandList();
                _uploadFences[i] = factory.CreateFence(false);
                _uploadSubmittedMeshes[i].Clear();
            }

            _stagingMeshPool = new ChunkStagingMeshPool(factory, RegionSize.Volume, _uploadReady.Length);

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

            foreach (ChunkMesh mesh in _meshes)
            {
                mesh.CreateDeviceObjects(gd, cl, sc);
            }

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
            foreach (ChunkMesh mesh in _meshes)
            {
                mesh.DestroyDeviceObjects();
            }

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                {
                    region.DestroyDeviceObjects();
                }
            }

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadReady[i] = false;
                _uploadLists[i].Dispose();
                _uploadFences[i].Dispose();
            }

            _stagingMeshPool.Dispose();
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

            ImGuiNET.ImGui.Begin("ChunkRenderer");
            {
                ImGuiNET.ImGui.Text("Triangle Count: " + (_lastTriangleCount / 1000) + "k");
                ImGuiNET.ImGui.Text("Draw Calls: " + _lastDrawCalls);

                int total = 0;
                int toBuild = 0;
                int toUpload = 0;
                int meshCount = 0;
                int regionCount = 0;

                lock (_regions)
                {
                    foreach (ChunkMeshRegion reg in _regions.Values)
                    {
                        (int Total, int ToBuild, int ToUpload) = reg.GetMeshCount();
                        total += Total;
                        toBuild += ToBuild;
                        toUpload += ToUpload;
                        regionCount++;
                    }
                }

                foreach (ChunkMesh mesh in _meshes)
                {
                    (int Total, int ToBuild, int ToUpload) = mesh.GetMeshCount();
                    total += Total;
                    toBuild += ToBuild;
                    toUpload += ToUpload;
                    meshCount++;
                }

                ImGuiNET.ImGui.Text("Chunks to build: " + toBuild);
                ImGuiNET.ImGui.Text("Chunks to upload: " + toUpload);
                ImGuiNET.ImGui.Text("Chunks: " + total);
                ImGuiNET.ImGui.Text("Chunk instances: " + meshCount);
                ImGuiNET.ImGui.Text("Chunk regions: " + regionCount);

                ImGuiNET.ImGui.NewLine();

                ImGuiNET.ImGui.Text(_graphicsBackendName);
                ImGuiNET.ImGui.Text(_graphicsDeviceName);
            }
            ImGuiNET.ImGui.End();
        }

        public void GatherVisibleChunks(
            SceneContext sc,
            Vector3? cullOrigin,
            BoundingFrustum? cullFrustum,
            List<ChunkMesh> visibleChunks)
        {
            using var profilerToken = sc.Profiler.Push();

            Vector3 origin = cullOrigin.GetValueOrDefault();

            if (cullFrustum.HasValue)
            {
                BoundingFrustum frustum = cullFrustum.GetValueOrDefault();

                foreach (ChunkMesh mesh in _meshes)
                {
                    Vector3 chunkPos = mesh.Position.ToBlock();
                    BoundingBox box = new(chunkPos, chunkPos + Chunk.Size);

                    if (frustum.Contains(box) != ContainmentType.Disjoint)
                    {
                        visibleChunks.Add(mesh);
                    }
                }
            }
            else
            {
                visibleChunks.AddRange(_meshes);
            }

            if (cullOrigin.HasValue)
            {
                visibleChunks.Sort((x, y) =>
                {
                    float a = ManhattanDistance(x.Position.ToBlock(), origin);
                    float b = ManhattanDistance(y.Position.ToBlock(), origin);
                    return a.CompareTo(b);
                });
            }
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
            List<ChunkMeshRegion> visibleRegions)
        {
            using var profilerToken = sc.Profiler.Push();

            lock (_regions)
            {
                if (cullFrustum.HasValue)
                {
                    BoundingFrustum frustum = cullFrustum.GetValueOrDefault();

                    foreach (ChunkMeshRegion region in _regions.Values)
                    {
                        Vector3 regionPos = region.Position.ToBlock(region.Size);
                        BoundingBox box = new(regionPos, regionPos + (region.Size * Chunk.Size));

                        if (frustum.Contains(box) != ContainmentType.Disjoint)
                        {
                            visibleRegions.Add(region);
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

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            using var profilerToken = sc.Profiler.Push();

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

            cl.UpdateBuffer(_worldInfoBuffer, 0, _worldInfo);

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

                RenderMeshes(gd, cl, sc, renderCameraInfoSet, cullOrigin, cullFrustum);
                RenderRegions(gd, cl, sc, renderCameraInfoSet, cullOrigin, cullFrustum);
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

        private void RenderRegions(
            GraphicsDevice gd, CommandList cl, SceneContext sc,
            ResourceSet cameraInfoSet, Vector3? cullOrigin, BoundingFrustum? cullFrustum)
        {
            using var profilerToken = sc.Profiler.Push();

            List<ChunkMeshRegion> visibleRegions = _visibleRegionBuffer;
            visibleRegions.Clear();
            GatherVisibleRegions(sc, cullOrigin, cullFrustum, visibleRegions);

            cl.SetPipeline(_indirectPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            int uploadCount = Render(gd, cl, sc, ref _regionBuildLimit, visibleRegions.GetEnumerator());
            _regionBuildLimit += uploadCount;
        }

        long lastbytesum = 0;

        private int Render<TMeshes>(
            GraphicsDevice gd, CommandList cl, SceneContext sc, ref int maxBuilds, TMeshes meshes)
            where TMeshes : IEnumerator<ChunkMeshBase>
        {
            using var profilerToken = sc.Profiler.Push();

            bool[] uploadReady = _uploadReady;
            int uploadOffset = 0;
            int uploadCount = 0;
            bool canUploadMore = true;

            while (meshes.MoveNext())
            {
                ChunkMeshBase? mesh = meshes.Current;

                if (maxBuilds > 0)
                {
                    if (mesh.IsBuildRequired && mesh.Build(ChunkMesher, _blockMemory))
                    {
                        maxBuilds--;
                    }
                }

                if (canUploadMore && mesh.IsUploadRequired)
                {
                    for (int j = 0; j < uploadReady.Length; j++)
                    {
                        int uploadIndex = (j + uploadOffset) % uploadReady.Length;

                        if (uploadReady[uploadIndex])
                        {
                            if (mesh.Upload(gd, _uploadLists[uploadIndex], _stagingMeshPool, out ChunkStagingMesh? stagingMesh))
                            {
                                uploadCount++;

                                if (stagingMesh != null)
                                {
                                    _uploadSubmittedMeshes[uploadIndex].Add(stagingMesh);
                                    uploadOffset++;
                                }
                            }
                            else
                            {
                                canUploadMore = false;
                            }
                            break;
                        }
                    }
                }

                mesh.Render(cl);

                int triCount = mesh.IndexCount / 3;
                _lastTriangleCount += triCount;

                if (triCount > 0)
                    _lastDrawCalls++;
            }

            long ss = (long)(ChunkStagingMesh.totalbytesum / (1024.0 * 1024.0 * 20)) * 20;
            if (lastbytesum != ss)
                Console.WriteLine(ss);
            lastbytesum = ss;

            return uploadCount;
        }

        private void RenderMeshes(
            GraphicsDevice gd, CommandList cl, SceneContext sc,
            ResourceSet cameraInfoSet, Vector3? cullOrigin, BoundingFrustum? cullFrustum)
        {
            using var profilerToken = sc.Profiler.Push();

            List<ChunkMesh> visibleMeshes = _visibleMeshBuffer;
            visibleMeshes.Clear();
            GatherVisibleChunks(sc, cullOrigin, cullFrustum, visibleMeshes);

            cl.SetPipeline(_directPipeline);
            cl.SetGraphicsResourceSet(0, cameraInfoSet);
            cl.SetGraphicsResourceSet(1, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            int uploadCount = Render(gd, cl, sc, ref _meshBuildLimit, visibleMeshes.GetEnumerator());
            _meshBuildLimit += uploadCount;
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
