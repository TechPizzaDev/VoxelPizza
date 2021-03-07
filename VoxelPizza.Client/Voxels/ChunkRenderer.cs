﻿using System;
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
    public class ChunkRenderer : Renderable, IUpdateable
    {
        private DisposeCollector _disposeCollector;

        private bool[] _uploadReady;
        private CommandList[] _uploadLists;
        private Fence[] _uploadFences;
        private ChunkStagingMeshPool _stagingMeshPool;
        private List<ChunkStagingMesh>[] _uploadSubmittedMeshes;
        private byte[][] _buildBuffers;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;

        private Pipeline _pipeline;
        private Pipeline _indirectPipeline;
        private ResourceSet _sharedSet;

        private List<Chunk> _chunks = new();

        private List<ChunkMesh> _visibleMeshBuffer = new();
        private List<ChunkMesh> _meshes = new();
        private ConcurrentQueue<ChunkMesh> _queuedMeshes = new();

        private List<ChunkMeshRegion> _visibleRegionBuffer = new();
        private Dictionary<ChunkRegionPosition, ChunkMeshRegion> _regions = new();
        private ConcurrentQueue<ChunkMeshRegion> _queuedRegions = new();

        private WorldInfo _worldInfo;

        private int _lastTriangleCount;
        private int _lastDrawCalls;

        public Camera Camera { get; }
        public Int3 RegionSize { get; }
        public ArrayPool<byte> ChunkMeshPool { get; private set; }

        public ResourceLayout ChunkInfoLayout { get; private set; }

        public int RegionVolume => RegionSize.X * RegionSize.Y * RegionSize.Z;

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public ChunkRenderer(Camera camera, Int3 regionSize)
        {
            if (regionSize.IsNegative())
                throw new ArgumentOutOfRangeException(nameof(regionSize));

            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            RegionSize = regionSize;
            ChunkMeshPool = ArrayPool<byte>.Shared;

            _uploadReady = new bool[4];
            _uploadLists = new CommandList[_uploadReady.Length];
            _uploadFences = new Fence[_uploadReady.Length];
            _uploadSubmittedMeshes = new List<ChunkStagingMesh>[_uploadReady.Length];
            _buildBuffers = new byte[_uploadReady.Length][];

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadSubmittedMeshes[i] = new List<ChunkStagingMesh>();
                _buildBuffers[i] = new byte[1024 * 1024 * 2];
            }

            StartThread();
        }

        public ChunkRegionPosition GetRegionPosition(ChunkPosition chunkPosition)
        {
            return new ChunkRegionPosition(
                chunkPosition.X / RegionSize.X,
                chunkPosition.Y / RegionSize.Y,
                chunkPosition.Z / RegionSize.Z);
        }

        public void StartThread()
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);

                int width = 32;
                int height = 5;

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
                    var chunk = new Chunk(new(x, y, z));
                    chunk.Generate();
                    _chunks.Add(chunk);

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
                    region.UpdateChunk(chunk);

                    //var mesh = new ChunkMesh(this, chunk);
                    //_queuedMeshes.Enqueue(mesh);

                    count++;
                    //if (count == 8)
                    //{
                    //    Thread.Sleep(1);
                    //    count = 0;
                    //}
                }
            });
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            DisposeCollectorResourceFactory factory = new(gd.ResourceFactory);
            _disposeCollector = factory.DisposeCollector;

            for (int i = 0; i < _uploadReady.Length; i++)
            {
                _uploadReady[i] = true;
                _uploadLists[i] = factory.CreateCommandList();
                _uploadFences[i] = factory.CreateFence(false);
                _uploadSubmittedMeshes[i].Clear();
            }

            _stagingMeshPool = new ChunkStagingMeshPool(factory, RegionVolume);

            ResourceLayout sharedLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("CameraInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex),
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

            VertexLayoutDescription worldLayout = new VertexLayoutDescription(
                new VertexElementDescription("Translation", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3))
            {
                InstanceStepRate = 1
            };

            (Shader mainVs, Shader mainFs, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkMain");
            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { spaceLayout, paintLayout },
                    new[] { mainVs, mainFs, },
                    mainSpecs),
                new[] { sharedLayout, ChunkInfoLayout },
                sc.MainSceneFramebuffer.OutputDescription));

            (Shader mainIndirectVs, Shader mainIndirectFs, SpecializationConstant[] mainIndirectSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ChunkIndirectMain", "ChunkMain");

            _indirectPipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { spaceLayout, paintLayout, worldLayout },
                    new[] { mainIndirectVs, mainIndirectFs, },
                    mainIndirectSpecs),
                new[] { sharedLayout },
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
                sc.CameraInfoBuffer,
                _worldInfoBuffer,
                sc.LightInfoBuffer,
                _textureAtlasBuffer));

            foreach (ChunkMesh mesh in _meshes)
                mesh.CreateDeviceObjects(gd, cl, sc);

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                    region.CreateDeviceObjects(gd, cl, sc);
            }
        }

        public override void DestroyDeviceObjects()
        {
            foreach (ChunkMesh mesh in _meshes)
                mesh.DestroyDeviceObjects();

            lock (_regions)
            {
                foreach (ChunkMeshRegion region in _regions.Values)
                    region.DestroyDeviceObjects();
            }

            _disposeCollector.DisposeAll();
        }

        public void Update(in FrameTime time)
        {
            _worldInfo.GlobalTime = time.TotalSeconds;

            ImGuiNET.ImGui.Begin("Stats");
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
            }

            while (_queuedRegions.TryDequeue(out ChunkMeshRegion? region))
            {
                region.CreateDeviceObjects(gd, cl, sc);
            }

            _lastTriangleCount = 0;
            _lastDrawCalls = 0;

            RenderMeshes(gd, cl, sc);
            RenderRegions(gd, cl, sc);

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
        }

        private void RenderRegions(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            List<ChunkMeshRegion> visibleRegions = _visibleRegionBuffer;
            visibleRegions.Clear();
            GatherVisibleRegions(visibleRegions);

            cl.SetPipeline(_indirectPipeline);
            cl.SetGraphicsResourceSet(0, _sharedSet);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);

            bool[] uploadReady = _uploadReady;
            int uploadOffset = 0;

            for (int i = 0; i < visibleRegions.Count; i++)
            {
                ChunkMeshRegion region = visibleRegions[i];

                region.Build(_buildBuffers[0]);

                if (region.UploadRequired)
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
        }

        private void RenderMeshes(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            List<ChunkMesh> visibleMeshes = _visibleMeshBuffer;
            visibleMeshes.Clear();
            GatherVisibleChunks(visibleMeshes);

            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _sharedSet);
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
