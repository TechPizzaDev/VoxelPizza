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
using VoxelPizza.Rendering.Voxels.Meshing;

namespace VoxelPizza.Client.Rendering.Voxels
{
    public class RenderRegionRenderer : Renderable, IUpdateable
    {
        private RenderRegionManager _manager;

        private ConcurrentQueue<RegionChange> _regionChanges = new();
        private Dictionary<RenderRegionPosition, VisualRegion> _regions = new();

        private GraphicsDevice _gd;

        private DeviceBuffer _worldInfoBuffer;
        private DeviceBuffer _textureAtlasBuffer;

        private Pipeline _directPipeline;
        private Pipeline _indirectPipeline;
        private ResourceSet _sharedSet;

        private FencedCommandList _stagingFCL;
        private DeviceBuffer _stagingBuffer;
        private List<RegionMeshBuffer> _stagingRegions = new();

        private HashSet<RenderRegionPosition> _regionsToAdd = new();
        private HashSet<RenderRegionPosition> _regionsToUpdate = new();
        private HashSet<RenderRegionPosition> _regionsToRemove = new();

        public ResourceLayout ChunkSharedLayout { get; private set; }
        public ResourceLayout ChunkInfoLayout { get; private set; }

        public Camera? RenderCamera { get; set; }
        public Camera? CullCamera { get; set; }

        public Size3 RegionSize => _manager.RegionSize;

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public RenderRegionRenderer(RenderRegionManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _gd = gd;

            _stagingFCL = new FencedCommandList(gd.ResourceFactory.CreateCommandList(), gd.ResourceFactory.CreateFence(true));

            _stagingBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(1024 * 1024 * 16, BufferUsage.StagingWrite));

            CreateRendererResources(gd, cl, sc);
        }

        public override void DestroyDeviceObjects()
        {
            _gd.WaitForIdle();

            DestroyRendererResources();

            _stagingFCL.Dispose();

            _stagingBuffer?.Dispose();

            foreach (RegionMeshBuffer staging in _stagingRegions)
            {
                staging.Buffers?.Dispose();
            }
            _stagingRegions.Clear();

            foreach (VisualRegion region in _regions.Values)
            {
                region.SetMeshBuffers(null);
            }
        }

        public void AddRegion(RenderRegionPosition regionPosition)
        {
            _regionChanges.Enqueue(new RegionChange(RegionChangeType.Add, regionPosition));
        }

        public void UpdateRegion(RenderRegionPosition regionPosition)
        {
            _regionChanges.Enqueue(new RegionChange(RegionChangeType.Update, regionPosition));
        }

        public void RemoveRegion(RenderRegionPosition regionPosition)
        {
            _regionChanges.Enqueue(new RegionChange(RegionChangeType.Remove, regionPosition));
        }

        public void Update(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            while (_regionChanges.TryDequeue(out RegionChange change))
            {
                switch (change.Type)
                {
                    case RegionChangeType.Add:
                        _regionsToAdd.Add(change.Region);
                        _regionsToRemove.Remove(change.Region);
                        break;

                    case RegionChangeType.Update:
                        _regionsToUpdate.Add(change.Region);
                        break;

                    case RegionChangeType.Remove:
                        _regionsToAdd.Remove(change.Region);
                        _regionsToRemove.Add(change.Region);
                        break;
                }
            }

            foreach (RenderRegionPosition regionPosition in _regionsToRemove)
            {
                // We processed as many events as possible.
                // If a region is now marked for removal, stop its update request.
                if (_regionsToUpdate.Count > 0)
                {
                    _regionsToUpdate.Remove(regionPosition);
                }

                if (_regions.Remove(regionPosition, out VisualRegion? region))
                {
                    // TODO: recycle region
                    region.Dispose();
                }
            }
            _regionsToRemove.Clear();

            foreach (RenderRegionPosition regionPosition in _regionsToAdd)
            {
                if (!_regions.TryGetValue(regionPosition, out VisualRegion? region))
                {
                    region = new VisualRegion(RegionSize);
                    region.SetPosition(regionPosition);
                    _regions.Add(regionPosition, region);
                }
            }
            _regionsToAdd.Clear();

            if (!_stagingFCL.Fence.Signaled)
            {
                return;
            }

            foreach (RegionMeshBuffer meshBuffer in _stagingRegions)
            {
                meshBuffer.Region.SetMeshBuffers(meshBuffer.Buffers);
            }
            _stagingRegions.Clear();

            _gd.ResetFence(_stagingFCL.Fence);
            _stagingFCL.CommandList.Begin();

            MappedResource mappedStagingBuffer = _gd.Map(_stagingBuffer, MapMode.Write);
            try
            {
                Span<byte> stagingBufferSpan = mappedStagingBuffer.AsBytes();
                uint stagingBufferOffset = 0;

                foreach (RenderRegionPosition regionPosition in _regionsToUpdate)
                {
                    if (_regions.TryGetValue(regionPosition, out VisualRegion? visualRegion) &&
                        _manager.TryGetLogicalRegion(regionPosition, out LogicalRegion? logicalRegion))
                    {
                        Span<byte> stagingBufferSlice = stagingBufferSpan.Slice((int)stagingBufferOffset);
                        if (!visualRegion.Encode(logicalRegion, stagingBufferSlice, out ChannelSizes channelSizes))
                        {
                            break;
                        }

                        ChunkMeshBuffers? meshBuffers = null;

                        if (channelSizes.TotalSize != 0)
                        {
                            ChunkMeshSizes meshSizes = new(
                                channelSizes.IndexSize / sizeof(uint),
                                channelSizes.IndirectSize,
                                channelSizes.RenderInfoSize,
                                channelSizes.IndexSize,
                                channelSizes.SpaceVertexSize,
                                channelSizes.PaintVertexSize);

                            meshBuffers = Copy(
                                _gd,
                                _stagingFCL.CommandList,
                                visualRegion.Position,
                                meshSizes,
                                _stagingBuffer,
                                stagingBufferOffset);

                            uint totalSize = channelSizes.TotalSize;
                            stagingBufferOffset += totalSize;
                        }

                        _stagingRegions.Add(new RegionMeshBuffer(visualRegion, meshBuffers));
                    }
                    _regionsToUpdate.Remove(regionPosition);
                }
            }
            finally
            {
                _gd.Unmap(_stagingBuffer);
            }

            _stagingFCL.CommandList.End();
            _gd.SubmitCommands(_stagingFCL.CommandList, _stagingFCL.Fence);
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            Camera? renderCamera = RenderCamera;
            if (renderCamera != null)
            {
                ResourceSet renderCameraInfoSet = sc.GetCameraInfoSet(renderCamera);

                cl.SetPipeline(_indirectPipeline);
                cl.SetGraphicsResourceSet(0, renderCameraInfoSet);
                cl.SetGraphicsResourceSet(1, _sharedSet);
                cl.SetFramebuffer(sc.MainSceneFramebuffer);

                RenderAllRegions(sc.Profiler, cl);
            }
        }

        private void RenderAllRegions(Profiler? profiler, CommandList cl)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            foreach (VisualRegion region in _regions.Values)
            {
                region.Render(cl);
            }
        }

        private void CreateRendererResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

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
        }


        private void DestroyRendererResources()
        {
            ChunkSharedLayout?.Dispose();
            ChunkInfoLayout?.Dispose();
            _directPipeline?.Dispose();
            _indirectPipeline?.Dispose();
            _worldInfoBuffer?.Dispose();
            _textureAtlasBuffer?.Dispose();
            _sharedSet?.Dispose();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey(ulong.MaxValue);
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }

        private enum RegionChangeType
        {
            Add,
            Update,
            Remove
        }

        private record struct RegionChange(RegionChangeType Type, RenderRegionPosition Region);

        private record struct RegionMeshBuffer(VisualRegion Region, ChunkMeshBuffers? Buffers);

        public static ChunkMeshBuffers Copy(
            GraphicsDevice graphicsDevice,
            CommandList uploadList,
            RenderRegionPosition position,
            ChunkMeshSizes sizes,
            DeviceBuffer sourceBuffer,
            uint sourceOffset)
        {
            ResourceFactory factory = graphicsDevice.ResourceFactory;

            ChunkMeshBuffers mesh = new();
            mesh.SyncPoint = Stopwatch.GetTimestamp();

            uint indirectSizeInBytes = (uint)(Unsafe.SizeOf<IndirectDrawIndexedArguments>() * sizes.DrawCount);
            mesh._indirectBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indirectSizeInBytes,
                Usage = BufferUsage.IndirectBuffer,
            });

            uint renderInfoSizeInBytes = (uint)(Unsafe.SizeOf<ChunkRenderInfo>() * sizes.DrawCount);
            mesh._renderInfoBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = renderInfoSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint indexSizeInBytes = sizes.IndexBytesRequired;
            mesh._indexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = indexSizeInBytes,
                Usage = BufferUsage.IndexBuffer,
            });

            uint spaceVertexSizeInBytes = (uint)(sizes.VertexCount * Unsafe.SizeOf<ChunkSpaceVertex>());
            mesh._spaceVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = spaceVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            uint paintVertexSizeInBytes = (uint)(sizes.VertexCount * Unsafe.SizeOf<ChunkPaintVertex>());
            mesh._paintVertexBuffer = factory.CreateBuffer(new BufferDescription()
            {
                SizeInBytes = paintVertexSizeInBytes,
                Usage = BufferUsage.VertexBuffer,
            });

            if (graphicsDevice.IsDebug)
            {
                mesh._indirectBuffer.Name = $"{nameof(World.ChunkRegion)}@{position}.{nameof(mesh._indirectBuffer)}";
                mesh._renderInfoBuffer.Name = $"{nameof(World.ChunkRegion)}@{position}.{nameof(mesh._renderInfoBuffer)}";
                mesh._indexBuffer.Name = $"{nameof(World.ChunkRegion)}@{position}.{nameof(mesh._indexBuffer)}";
                mesh._spaceVertexBuffer.Name = $"{nameof(World.ChunkRegion)}@{position}.{nameof(mesh._spaceVertexBuffer)}";
                mesh._paintVertexBuffer.Name = $"{nameof(World.ChunkRegion)}@{position}.{nameof(mesh._paintVertexBuffer)}";
            }

            bool debugMarkers = graphicsDevice.Features.CommandListDebugMarkers;
            if (debugMarkers)
            {
                uploadList.PushDebugGroup($"Upload of {nameof(World.ChunkRegion)}@{position}");
            }

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._indirectBuffer, 0, sizes.IndirectBytesRequired);
            sourceOffset += sizes.IndirectBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._renderInfoBuffer, 0, sizes.RenderInfoBytesRequired);
            sourceOffset += sizes.RenderInfoBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._indexBuffer, 0, sizes.IndexBytesRequired);
            sourceOffset += sizes.IndexBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._spaceVertexBuffer, 0, sizes.SpaceVertexBytesRequired);
            sourceOffset += sizes.SpaceVertexBytesRequired;

            uploadList.CopyBuffer(sourceBuffer, sourceOffset, mesh._paintVertexBuffer, 0, sizes.PaintVertexBytesRequired);
            sourceOffset += sizes.PaintVertexBytesRequired;

            if (debugMarkers)
            {
                uploadList.PopDebugGroup();
            }

            mesh.DrawCount = sizes.DrawCount;
            mesh.IndexCount = sizes.IndexCount;
            mesh.VertexCount = sizes.VertexCount;

            return mesh;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WorldInfo
            {
            public float GlobalTime;

            private float _padding0;
            private float _padding1;
            private float _padding2;
        }
    }
}
