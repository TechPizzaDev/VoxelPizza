using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public partial class ChunkBorderRenderer : Renderable, IUpdateable
    {
        private GeometryBatch<VertexPosition<RgbaByte>> _chunkBatch;
        private GeometryBatch<VertexPosition<RgbaByte>> _chunkRegionBatch;
        private HashSet<ChunkPosition> _chunks;
        private HashSet<ChunkRegionPosition> _chunkRegions;
        private bool _chunksNeedUpdate;
        private bool _regionsNeedUpdate;

        private Pipeline _batchDepthLessPipeline;
        private Pipeline _batchDepthPipeline;

        public ChunkRenderer ChunkRenderer { get; }

        public bool DrawChunks { get; set; }
        public bool DrawRegions { get; set; }
        public bool UseDepth { get; set; }

        public override RenderPasses RenderPasses => UseDepth ? RenderPasses.Opaque : RenderPasses.AlphaBlend;

        public ChunkBorderRenderer(ChunkRenderer chunkRenderer)
        {
            ChunkRenderer = chunkRenderer ?? throw new ArgumentNullException(nameof(chunkRenderer));

            uint chunkQuadCap = 1024 * 32;
            _chunkBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * chunkQuadCap, 4 * chunkQuadCap);

            uint regionQuadCap = 1024 * 8;
            _chunkRegionBatch = new GeometryBatch<VertexPosition<RgbaByte>>(6 * regionQuadCap, 4 * regionQuadCap);

            _chunks = new HashSet<ChunkPosition>();
            _chunkRegions = new HashSet<ChunkRegionPosition>();

            ChunkRenderer.ChunkAdded += ChunkRenderer_ChunkAdded;
            ChunkRenderer.ChunkRemoved += ChunkRenderer_ChunkRemoved;

            ChunkRenderer.ChunkRegionAdded += ChunkRenderer_ChunkRegionAdded;
            ChunkRenderer.ChunkRegionRemoved += ChunkRenderer_ChunkRegionRemoved;
        }

        private void ChunkRenderer_ChunkAdded(Chunk chunk)
        {
            lock (_chunks)
            {
                _chunks.Add(chunk.Position);
                _chunksNeedUpdate = true;
            }
        }

        private void ChunkRenderer_ChunkRemoved(Chunk chunk)
        {
            lock (_chunks)
            {
                _chunks.Remove(chunk.Position);
                _chunksNeedUpdate = true;
            }
        }

        private void ChunkRenderer_ChunkRegionAdded(ChunkMeshRegion chunkRegion)
        {
            lock (_chunkRegions)
            {
                _chunkRegions.Add(chunkRegion.Position);
                _regionsNeedUpdate = true;
            }
        }

        private void ChunkRenderer_ChunkRegionRemoved(ChunkMeshRegion chunkRegion)
        {
            lock (_chunkRegions)
            {
                _chunkRegions.Remove(chunkRegion.Position);
                _regionsNeedUpdate = true;
            }
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _chunkBatch.CreateDeviceObjects(gd, cl, sc);
            _chunkRegionBatch.CreateDeviceObjects(gd, cl, sc);

            ResourceFactory factory = gd.ResourceFactory;

            VertexLayoutDescription vertexLayout = new(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Byte4));

            (Shader colorVs, Shader colorFs, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ColorMain");

            var rasterizerState = RasterizerStateDescription.Default;

            var depthStencilState = gd.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual;

            var pipelineDesc = new GraphicsPipelineDescription(
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
        }

        public override void DestroyDeviceObjects()
        {
            _chunkBatch.DestroyDeviceObjects();
            _chunkRegionBatch.DestroyDeviceObjects();
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
            if (!DrawChunks && !DrawRegions)
            {
                return;
            }

            Camera? renderCamera = ChunkRenderer.RenderCamera;
            if (renderCamera == null)
            {
                return;
            }

            cl.SetPipeline(UseDepth ? _batchDepthPipeline : _batchDepthLessPipeline);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            cl.SetFullViewport(0);
            cl.SetGraphicsResourceSet(0, sc.GetCameraInfoSet(renderCamera));

            if (DrawChunks)
                _chunkBatch.Submit(cl);

            if (DrawRegions)
                _chunkRegionBatch.Submit(cl);
        }

        [SkipLocalsInit]
        public unsafe void Update(in FrameTime time)
        {
            Span<uint> indices = stackalloc uint[ShapeMeshHelper.BoxIndexCount];
            Span<VertexPosition<RgbaByte>> vertices = stackalloc VertexPosition<RgbaByte>[ShapeMeshHelper.BoxMaxVertexCount];

            if (DrawChunks && _chunksNeedUpdate)
            {
                UpdateChunkBatch(indices, vertices);
                _chunksNeedUpdate = false;
            }

            if (DrawRegions && _regionsNeedUpdate)
            {
                UpdateChunkRegionBatch(indices, vertices);
                _regionsNeedUpdate = false;
            }
        }

        private unsafe void UpdateChunkBatch(Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            float lineWidth = 0.125f;
            RgbaByte color0 = new(0, 255, 0, 255);
            RgbaByte color1 = new(255, 0, 0, 255);

            _chunkBatch.Begin();

            foreach (ChunkPosition chunk in _chunks)
            {
                int vertexCount = ShapeMeshHelper.GetBoxMesh(
                    chunk.ToBlock(), Chunk.Size, lineWidth,
                    color0, color1,
                    indices, vertices);

                var reserve = _chunkBatch.ReserveUnsafe(indices.Length, vertexCount);
                vertices.Slice(0, vertexCount).CopyTo(new Span<VertexPosition<RgbaByte>>(reserve.Vertices, vertexCount));

                Span<uint> reserveIndices = new(reserve.Indices, indices.Length);
                for (int i = 0; i < indices.Length; i++)
                {
                    reserveIndices[i] = indices[i] + reserve.VertexOffset;
                }
            }

            _chunkBatch.End();
        }

        private unsafe void UpdateChunkRegionBatch(Span<uint> indices, Span<VertexPosition<RgbaByte>> vertices)
        {
            float lineWidth = 0.125f;
            RgbaByte color0 = new(0, 127, 255, 255);
            RgbaByte color1 = new(127, 0, 255, 255);

            Size3 regionSize = ChunkRenderer.RegionSize;
            Size3f meshSize = regionSize * Chunk.Size;

            _chunkRegionBatch.Begin();

            foreach (ChunkRegionPosition chunkRegion in _chunkRegions)
            {
                int vertexCount = ShapeMeshHelper.GetBoxMesh(
                    chunkRegion.ToBlock(regionSize), meshSize, lineWidth,
                    color0, color1,
                    indices, vertices);

                var reserve = _chunkRegionBatch.ReserveUnsafe(indices.Length, vertexCount);
                vertices.Slice(0, vertexCount).CopyTo(new Span<VertexPosition<RgbaByte>>(reserve.Vertices, vertexCount));

                Span<uint> reserveIndices = new(reserve.Indices, indices.Length);
                for (int i = 0; i < indices.Length; i++)
                {
                    reserveIndices[i] = indices[i] + reserve.VertexOffset;
                }
            }

            _chunkRegionBatch.End();
        }
    }
}
