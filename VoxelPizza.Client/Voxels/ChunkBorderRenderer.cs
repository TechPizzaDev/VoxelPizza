using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkBorderRenderer : Renderable, IUpdateable
    {
        private enum EventType
        {
            Add, Update, Remove
        }

        private record struct Event<T>(EventType Type, T Position);

        private GeometryBatch<VertexPosition<RgbaByte>> _cameraBatch;
        private GeometryBatch<VertexPosition<RgbaByte>> _chunkBatch;
        private GeometryBatch<VertexPosition<RgbaByte>> _chunkRegionBatch;
        private GeometryBatch<VertexPosition<RgbaByte>> _renderRegionBatch;
        private HashSet<ChunkPosition> _chunks = new();
        private HashSet<ChunkRegionPosition> _chunkRegions = new();
        private HashSet<RenderRegionPosition> _renderRegions = new();
        private bool _chunksNeedUpdate;
        private bool _chunkRegionsNeedUpdate;
        private bool _renderRegionsNeedUpdate;

        private ConcurrentQueue<Event<ChunkPosition>> _chunkEvents = new();
        private ConcurrentQueue<Event<ChunkRegionPosition>> _chunkRegionEvents = new();
        private ConcurrentQueue<Event<RenderRegionPosition>> _renderRegionEvents = new();

        private Size3 _regionSize;
        private Camera? _cullCamera;
        private Camera? _renderCamera;

        private Pipeline _batchDepthLessPipeline;
        private Pipeline _batchDepthPipeline;

        public bool DrawCameraBounds { get; set; }
        public bool DrawChunks { get; set; }
        public bool DrawChunkRegions { get; set; }
        public bool DrawRenderRegions { get; set; }
        public bool UseDepth { get; set; }

        public override RenderPasses RenderPasses => UseDepth ? RenderPasses.Opaque : RenderPasses.AlphaBlend;

        public ChunkBorderRenderer()
        {
            uint cameraQuadCap = 128;
            _cameraBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * cameraQuadCap, 4 * cameraQuadCap);

            uint chunkQuadCap = 1024 * 32;
            _chunkBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * chunkQuadCap, 4 * chunkQuadCap);

            uint chunkRegionQuadCap = 1024 * 4;
            _chunkRegionBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * chunkRegionQuadCap, 4 * chunkRegionQuadCap);

            uint renderRegionQuadCap = 1024 * 8;
            _renderRegionBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * renderRegionQuadCap, 4 * renderRegionQuadCap);
        }

        public void RegisterDimension(Dimension dimension)
        {
            dimension.ChunkAdded += ChunkRenderer_ChunkAdded;
            dimension.ChunkUpdated += ChunkRenderer_ChunkUpdated;
            dimension.ChunkRemoved += ChunkRenderer_ChunkRemoved;

            dimension.RegionAdded += ChunkRenderer_RegionAdded;
            dimension.RegionRemoved += ChunkRenderer_RegionRemoved;
        }

        public void RegisterChunkRenderer(
            Size3 regionSize,
            Camera? cullCamera,
            Camera? renderCamera)
        {
            _regionSize = regionSize;
            _cullCamera = cullCamera;
            _renderCamera = renderCamera;

            //ChunkRenderer.RenderRegionAdded += ChunkRenderer_RenderRegionAdded;
            //ChunkRenderer.RenderRegionRemoved += ChunkRenderer_RenderRegionRemoved;
        }

        private void ChunkRenderer_ChunkAdded(Chunk chunk)
        {
            if (!DrawChunks)
                return;

            if (!chunk.IsEmpty)
                _chunkEvents.Enqueue(new(EventType.Add, chunk.Position));
        }

        private void ChunkRenderer_ChunkUpdated(Chunk chunk)
        {
            if (!DrawChunks)
                return;

            if (!chunk.IsEmpty)
            {
                _chunkEvents.Enqueue(new(EventType.Update, chunk.Position));
            }
            else
            {
                _chunkEvents.Enqueue(new(EventType.Remove, chunk.Position));
            }
        }

        private void ChunkRenderer_ChunkRemoved(Chunk chunk)
        {
            if (DrawChunks)
                _chunkEvents.Enqueue(new(EventType.Remove, chunk.Position));
        }

        private void ChunkRenderer_RegionAdded(ChunkRegion chunkRegion)
        {
            if (DrawChunkRegions)
                _chunkRegionEvents.Enqueue(new(EventType.Add, chunkRegion.Position));
        }

        private void ChunkRenderer_RegionRemoved(ChunkRegion chunkRegion)
        {
            if (DrawChunkRegions)
                _chunkRegionEvents.Enqueue(new(EventType.Remove, chunkRegion.Position));
        }

        //private void ChunkRenderer_RenderRegionAdded(ChunkMeshRegion chunkRegion)
        //{
        //    if (DrawRenderRegions)
        //        _renderRegionEvents.Enqueue(new(EventType.Add, chunkRegion.Position));
        //}
        //
        //private void ChunkRenderer_RenderRegionRemoved(ChunkMeshRegion chunkRegion)
        //{
        //    if (DrawRenderRegions)
        //        _renderRegionEvents.Enqueue(new(EventType.Remove, chunkRegion.Position));
        //}

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _cameraBatch.CreateDeviceObjects(gd, cl, sc);
            _chunkBatch.CreateDeviceObjects(gd, cl, sc);
            _chunkRegionBatch.CreateDeviceObjects(gd, cl, sc);
            _renderRegionBatch.CreateDeviceObjects(gd, cl, sc);

            ResourceFactory factory = gd.ResourceFactory;

            VertexLayoutDescription vertexLayout = new(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Byte4_Norm));

            (Shader colorVs, Shader colorFs, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ColorMain");

            RasterizerStateDescription rasterizerState = RasterizerStateDescription.Default;

            DepthStencilStateDescription depthStencilState = gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual;

            GraphicsPipelineDescription pipelineDesc = new(
                BlendStateDescription.SingleOverrideBlend,
                depthStencilState,
                rasterizerState,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[] { vertexLayout },
                    new[] { colorVs, colorFs, },
                    mainSpecs),
                new[] { sc.CameraInfoLayout },
                sc.MainSceneFramebuffer.OutputDescription);

            _batchDepthPipeline = factory.CreateGraphicsPipeline(pipelineDesc);

            pipelineDesc.DepthStencilState = DepthStencilStateDescription.Disabled;
            _batchDepthLessPipeline = factory.CreateGraphicsPipeline(pipelineDesc);

            _chunksNeedUpdate = true;
            _chunkRegionsNeedUpdate = true;
            _renderRegionsNeedUpdate = true;
        }

        public override void DestroyDeviceObjects()
        {
            _batchDepthPipeline.Dispose();
            _batchDepthLessPipeline.Dispose();

            _cameraBatch.DestroyDeviceObjects();
            _chunkBatch.DestroyDeviceObjects();
            _chunkRegionBatch.DestroyDeviceObjects();
            _renderRegionBatch.DestroyDeviceObjects();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey();
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            if (!DrawCameraBounds && !DrawChunks && !DrawChunkRegions && !DrawRenderRegions)
            {
                return;
            }

            Camera? renderCamera = _renderCamera;
            if (renderCamera == null)
            {
                return;
            }

            cl.SetPipeline(UseDepth ? _batchDepthPipeline : _batchDepthLessPipeline);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            cl.SetFullViewport(0);
            cl.SetGraphicsResourceSet(0, sc.GetCameraInfoSet(renderCamera));

            if (DrawCameraBounds)
            {
                UpdateCameraBatch();
                _cameraBatch.Submit(cl);
            }

            if (DrawChunks)
                _chunkBatch.Submit(cl);

            if (DrawChunkRegions)
                _chunkRegionBatch.Submit(cl);

            if (DrawRenderRegions)
                _renderRegionBatch.Submit(cl);
        }

        [SkipLocalsInit]
        public unsafe void Update(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = state.Profiler.Push();

            Span<uint> indices = stackalloc uint[ShapeMeshHelper.BoxIndexCount];
            Span<VertexPosition<RgbaByte>> vertices = stackalloc VertexPosition<RgbaByte>[ShapeMeshHelper.BoxMaxVertexCount];

            if (DrawChunks)
            {
                _chunksNeedUpdate |= PumpEvents(_chunkEvents, _chunks, state.Profiler);
                if (_chunksNeedUpdate)
                {
                    UpdateChunkBatch(indices, vertices);
                    _chunksNeedUpdate = false;
                }
            }

            if (DrawChunkRegions)
            {
                _chunkRegionsNeedUpdate |= PumpEvents(_chunkRegionEvents, _chunkRegions, state.Profiler);
                if (_chunkRegionsNeedUpdate)
                {
                    UpdateChunkRegionBatch(indices, vertices);
                    _chunkRegionsNeedUpdate = false;
                }
            }

            if (DrawRenderRegions)
            {
                _renderRegionsNeedUpdate |= PumpEvents(_renderRegionEvents, _renderRegions, state.Profiler);
                if (_renderRegionsNeedUpdate)
                {
                    UpdateRenderRegionBatch(indices, vertices);
                    _renderRegionsNeedUpdate = false;
                }
            }
        }

        private static bool PumpEvents<T>(ConcurrentQueue<Event<T>> queue, HashSet<T> set, Profiler? profiler)
        {
            using ProfilerPopToken profilerToken = profiler.Push();

            bool changed = false;

            while (queue.TryDequeue(out Event<T> ev))
            {
                switch (ev.Type)
                {
                    case EventType.Add:
                    case EventType.Update:
                        if (set.Add(ev.Position))
                            changed = true;
                        break;

                    case EventType.Remove:
                        if (set.Remove(ev.Position))
                            changed = true;
                        break;
                }
            }

            return changed;
        }

        private unsafe void UpdateCameraBatch()
        {
            Camera? camera = _cullCamera;
            if (camera == null)
            {
                return;
            }

            _cameraBatch.Begin();

            BoundingFrustum4 cullFrustum = new(camera.ViewMatrix * camera.ProjectionMatrix);
            cullFrustum.GetCorners(out FrustumCorners4 corners);

            _cameraBatch.AppendQuad(
                new VertexPosition<RgbaByte>() { Position = corners.FarTopRight.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarTopLeft.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarBottomLeft.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarBottomRight.ToVector3(), Data = RgbaByte.White });

            _cameraBatch.AppendQuad(
                new VertexPosition<RgbaByte>() { Position = corners.NearTopLeft.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarTopLeft.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarBottomLeft.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.NearBottomLeft.ToVector3(), Data = RgbaByte.White });

            _cameraBatch.AppendQuad(
                new VertexPosition<RgbaByte>() { Position = corners.NearTopRight.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarTopRight.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.FarBottomRight.ToVector3(), Data = RgbaByte.White },
                new VertexPosition<RgbaByte>() { Position = corners.NearBottomRight.ToVector3(), Data = RgbaByte.White });

            //var reserve= _cameraBatch.ReserveUnsafe(3, 3);

            //reserve.Indices[0] = 0;
            //reserve.Indices[1] = 1;
            //reserve.Indices[2] = 2;
            //
            //reserve.Vertices[0] = new VertexPosition<RgbaByte>() { Position = new Vector3(0, 0, 0), Data = RgbaByte.White };
            //reserve.Vertices[1] = new VertexPosition<RgbaByte>() { Position = new Vector3(20, 0, 0), Data = RgbaByte.White };
            //reserve.Vertices[2] = new VertexPosition<RgbaByte>() { Position = new Vector3(0, 20, 0), Data = RgbaByte.White };

            _cameraBatch.End();
        }

        private void UpdateChunkBatch(Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            float lineWidth = 0.125f;
            RgbaByte color0 = new(0, 1, 0, 255);
            RgbaByte color1 = new(255, 0, 0, 255);

            Size3f meshSize = Chunk.Size;

            _chunkBatch.Begin();

            foreach (ChunkPosition chunk in _chunks)
            {
                Vector3 position = chunk.ToBlock();
                UpdateBatchItem(_chunkBatch, position, meshSize, lineWidth, color0, color1, indices, vertices);
            }

            _chunkBatch.End();
        }

        private void UpdateChunkRegionBatch(Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            float lineWidth = 0.175f;
            RgbaByte color0 = new(0, 127, 255, 255);
            RgbaByte color1 = new(127, 0, 255, 255);

            Size3f meshSize = ChunkRegion.Size * Chunk.Size;

            _chunkRegionBatch.Begin();

            foreach (ChunkRegionPosition chunkRegion in _chunkRegions)
            {
                Vector3 position = chunkRegion.ToChunk().ToBlock();
                UpdateBatchItem(_chunkRegionBatch, position, meshSize, lineWidth, color0, color1, indices, vertices);
            }

            _chunkRegionBatch.End();
        }

        private void UpdateRenderRegionBatch(Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            Size3 regionSize = _regionSize;
            if (regionSize == Size3.Zero)
            {
                _renderRegionBatch.Clear();
                return;
            }

            float lineWidth = 0.175f;
            RgbaByte color0 = new(0, 127, 255, 255);
            RgbaByte color1 = new(127, 0, 255, 255);

            Size3f meshSize = regionSize * Chunk.Size;

            _renderRegionBatch.Begin();

            foreach (RenderRegionPosition chunkRegion in _renderRegions)
            {
                Vector3 position = chunkRegion.ToBlock(regionSize);
                UpdateBatchItem(_renderRegionBatch, position, meshSize, lineWidth, color0, color1, indices, vertices);
            }

            _renderRegionBatch.End();
        }

        private static unsafe void UpdateBatchItem(
            GeometryBatch<VertexPosition<RgbaByte>> batch, Vector3 position, Size3f meshSize,
            float lineWidth, RgbaByte color0, RgbaByte color1,
            Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            int vertexCount = ShapeMeshHelper.GetBoxMesh(
                position, meshSize, lineWidth,
                color0, color1,
                indices, vertices);

            GeometryBatch<VertexPosition<RgbaByte>>.UnsafeReserve reserve = batch.ReserveUnsafe(indices.Length, vertexCount);
            vertices[..vertexCount].CopyTo(new Span<VertexPosition<RgbaByte>>(reserve.Vertices, vertexCount));

            Span<uint> reserveIndices = new(reserve.Indices, indices.Length);
            for (int i = 0; i < indices.Length; i++)
            {
                reserveIndices[i] = indices[i] + reserve.VertexOffset;
            }
        }
    }
}
