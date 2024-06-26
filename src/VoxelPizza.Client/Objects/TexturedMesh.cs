using System;
using System.Numerics;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Utilities;
using VoxelPizza.Client.Resources;

namespace VoxelPizza.Client.Objects
{
    public class TexturedMesh : CullRenderable
    {
        private readonly string _name;
        private readonly ConstructedMesh _meshData;
        private readonly ImageSharpTexture? _textureData;
        private readonly ImageSharpTexture? _alphaTextureData;
        private readonly Transform _transform;

        private BoundingBox _centeredBounds;
        private DeviceBuffer _vb;
        private DeviceBuffer _ib;
        private int _indexCount;
        private Texture _texture;
        private Texture _alphamapTexture;
        private TextureView _alphaMapView;

        private Pipeline _pipeline;
        private ResourceSet _mainSharedRS;
        private ResourceSet _mainPerObjectRS;
        private Pipeline _shadowMapPipeline;
        private ResourceSet[] _shadowMapResourceSets;

        private bool _transformDirty = true;
        private DeviceBuffer _worldAndInverseBuffer;

        private readonly DisposeCollector _disposeCollector = new();

        private readonly Vector3 _objectCenter;

        public Transform Transform => _transform;

        public TexturedMesh(
            string name,
            ConstructedMesh meshData,
            ImageSharpTexture? textureData,
            ImageSharpTexture? alphaTexture)
        {
            _name = name;
            _meshData = meshData;
            _centeredBounds = meshData.GetBoundingBox();
            _objectCenter = _centeredBounds.GetCenter();
            _textureData = textureData;
            _alphaTextureData = alphaTexture;

            _transform = new Transform();
            _transform.TransformChanged += Transform_TransformChanged;
        }

        private void Transform_TransformChanged(Transform transform)
        {
            _transformDirty = true;
        }

        public override BoundingBox BoundingBox => BoundingBox.Transform(_centeredBounds, _transform.GetTransformMatrix());

        public override unsafe void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory disposeFactory = new DisposeCollectorResourceFactory(gd.ResourceFactory, _disposeCollector);
            _vb = _meshData.CreateVertexBuffer(disposeFactory, cl);
            _vb.Name = _name + "_VB";
            _ib = _meshData.CreateIndexBuffer(disposeFactory, cl);
            _ib.Name = _name + "_IB";
            _indexCount = _meshData.IndexCount;

            uint bufferSize = 128;
            _worldAndInverseBuffer = disposeFactory.CreateBuffer(new BufferDescription(bufferSize, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));

            if (_textureData != null)
            {
                _texture = StaticResourceCache.GetTexture2D(gd, gd.ResourceFactory, _textureData);
            }
            else
            {
                _texture = disposeFactory.CreateTexture(TextureDescription.Texture2D(1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
                RgbaByte color = RgbaByte.Pink;
                gd.UpdateTexture(_texture, (IntPtr)(&color), 4, 0, 0, 0, 1, 1, 1, 0, 0);
            }

            if (_alphaTextureData != null)
            {
                _alphamapTexture = _alphaTextureData.CreateDeviceTexture(gd, disposeFactory);
            }
            else
            {
                _alphamapTexture = StaticResourceCache.GetPinkTexture(gd, gd.ResourceFactory);
            }
            _alphaMapView = StaticResourceCache.GetTextureView(gd.ResourceFactory, _alphamapTexture);

            VertexLayoutDescription[] shadowDepthVertexLayouts = new VertexLayoutDescription[]
            {
                new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
            };

            (Shader depthVS, Shader depthFS, SpecializationConstant[] depthSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ShadowDepth");

            ResourceLayout projViewCombinedLayout = StaticResourceCache.GetResourceLayout(
                gd.ResourceFactory,
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("ViewProjection", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout worldLayout = StaticResourceCache.GetResourceLayout(gd.ResourceFactory, new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("WorldAndInverse", ResourceKind.UniformBuffer, ShaderStages.Vertex, ResourceLayoutElementOptions.DynamicBinding)));

            GraphicsPipelineDescription depthPD = new(
                BlendStateDescription.Empty,
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                new RasterizerStateDescription(FaceCullMode.Back, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(shadowDepthVertexLayouts, new[] { depthVS, depthFS }, depthSpecs),
                new ResourceLayout[] { projViewCombinedLayout, worldLayout },
                sc.NearShadowMapFramebuffer.OutputDescription);
            _shadowMapPipeline = StaticResourceCache.GetPipeline(gd.ResourceFactory, ref depthPD);

            _shadowMapResourceSets = CreateShadowMapResourceSets(gd.ResourceFactory, disposeFactory, sc, projViewCombinedLayout, worldLayout);

            VertexLayoutDescription[] mainVertexLayouts = new VertexLayoutDescription[]
            {
                new VertexLayoutDescription(
                    new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                    new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
            };

            (Shader mainVS, Shader mainFS, SpecializationConstant[] mainSpecs) =
                StaticResourceCache.GetShaders(gd, gd.ResourceFactory, "ShadowMain");

            ResourceLayout mainSharedLayout = StaticResourceCache.GetResourceLayout(gd.ResourceFactory, new ResourceLayoutDescription(
                new("LightViewProjection1", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                new("LightViewProjection2", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                new("LightViewProjection3", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                new("DepthLimits", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                new("LightInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
                new("PointLights", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)));

            ResourceLayout mainPerObjectLayout = StaticResourceCache.GetResourceLayout(gd.ResourceFactory, new ResourceLayoutDescription(
                new("WorldAndInverse", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment, ResourceLayoutElementOptions.DynamicBinding),
                new("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("RegularSampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new("AlphaMap", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("AlphaMapSampler", ResourceKind.Sampler, ShaderStages.Fragment),
                new("ShadowMapNear", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("ShadowMapMid", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("ShadowMapFar", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("ShadowMapSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            BlendStateDescription alphaBlendDesc = BlendStateDescription.SingleAlphaBlend;
            alphaBlendDesc.AlphaToCoverageEnabled = true;

            GraphicsPipelineDescription mainPD = new(
                _alphamapTexture != null ? alphaBlendDesc : BlendStateDescription.SingleOverrideBlend,
                gd.IsDepthRangeZeroToOne ? DepthStencilStateDescription.DepthOnlyGreaterEqual : DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(mainVertexLayouts, new[] { mainVS, mainFS }, mainSpecs),
                new ResourceLayout[] { mainSharedLayout, sc.CameraInfoLayout, mainPerObjectLayout },
                sc.MainSceneFramebuffer.OutputDescription);
            _pipeline = StaticResourceCache.GetPipeline(gd.ResourceFactory, ref mainPD);
            _pipeline.Name = "TexturedMesh Main Pipeline";

            _mainSharedRS = StaticResourceCache.GetResourceSet(gd.ResourceFactory, new ResourceSetDescription(mainSharedLayout,
                sc.LightViewProjectionBuffer0,
                sc.LightViewProjectionBuffer1,
                sc.LightViewProjectionBuffer2,
                sc.DepthLimitsBuffer,
                sc.LightInfoBuffer,
                sc.PointLightsBuffer));

            _mainPerObjectRS = disposeFactory.CreateResourceSet(new ResourceSetDescription(mainPerObjectLayout,
                _worldAndInverseBuffer,
                _texture,
                gd.Aniso4xSampler,
                _alphaMapView,
                gd.LinearSampler,
                sc.NearShadowMapView,
                sc.MidShadowMapView,
                sc.FarShadowMapView,
                gd.LinearSampler));

            _transformDirty = true;
        }

        private ResourceSet[] CreateShadowMapResourceSets(
            ResourceFactory sharedFactory,
            ResourceFactory disposeFactory,
            SceneContext sc,
            ResourceLayout projViewLayout,
            ResourceLayout worldLayout)
        {
            ResourceSet[] ret = new ResourceSet[6];

            for (int i = 0; i < 3; i++)
            {
                DeviceBuffer viewProjBuffer =
                    i == 0 ? sc.LightViewProjectionBuffer0 :
                    i == 1 ? sc.LightViewProjectionBuffer1 :
                    sc.LightViewProjectionBuffer2;

                ret[i * 2] = StaticResourceCache.GetResourceSet(sharedFactory, new ResourceSetDescription(
                    projViewLayout,
                    viewProjBuffer));

                ResourceSet worldRS = disposeFactory.CreateResourceSet(new ResourceSetDescription(
                    worldLayout,
                    _worldAndInverseBuffer));

                ret[i * 2 + 1] = worldRS;
            }

            return ret;
        }

        public override void DestroyDeviceObjects()
        {
            _disposeCollector.DisposeAll();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return RenderOrderKey.Create(
                _pipeline.GetHashCode(),
                Vector3.Distance((_objectCenter * _transform.Scale) + _transform.Position, cameraPosition));
        }

        public override RenderPasses RenderPasses
        {
            get
            {
                if (_alphaTextureData != null)
                {
                    return RenderPasses.AllShadowMap | RenderPasses.AlphaBlend;
                }
                else
                {
                    return RenderPasses.AllShadowMap | RenderPasses.Opaque;
                }
            }
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            if ((renderPass & RenderPasses.AllShadowMap) != 0)
            {
                int shadowMapIndex = renderPass == RenderPasses.ShadowMapNear ? 0 : renderPass == RenderPasses.ShadowMapMid ? 1 : 2;
                RenderShadowMap(cl, sc, shadowMapIndex);
            }
            else if (renderPass == RenderPasses.Opaque || renderPass == RenderPasses.AlphaBlend)
            {
                RenderStandard(cl, sc);
            }
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            if (_transformDirty)
            {
                _transformDirty = false;

                WorldAndInverse wai;
                wai.World = _transform.GetTransformMatrix();
                Matrix4x4.Invert(wai.World, out Matrix4x4 invertedWorld);
                wai.InverseWorld = Matrix4x4.Transpose(invertedWorld);
                cl.UpdateBuffer(_worldAndInverseBuffer, 0, ref wai);
            }
        }

        private void RenderShadowMap(CommandList cl, SceneContext sc, int shadowMapIndex)
        {
            if (sc.CurrentCamera == null)
            {
                return;
            }

            cl.SetPipeline(_shadowMapPipeline);
            cl.SetGraphicsResourceSet(0, _shadowMapResourceSets[shadowMapIndex * 2]);
            cl.SetGraphicsResourceSet(1, _shadowMapResourceSets[shadowMapIndex * 2 + 1]);

            cl.SetVertexBuffer(0, _vb);
            cl.SetIndexBuffer(_ib, _meshData.IndexFormat);
            cl.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
        }

        private void RenderStandard(CommandList cl, SceneContext sc)
        {
            Camera? camera = sc.CurrentCamera;
            if (camera == null)
            {
                return;
            }

            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _mainSharedRS);
            cl.SetGraphicsResourceSet(1, sc.GetCameraInfoSet(camera));
            cl.SetGraphicsResourceSet(2, _mainPerObjectRS);

            cl.SetVertexBuffer(0, _vb);
            cl.SetIndexBuffer(_ib, _meshData.IndexFormat);
            cl.DrawIndexed((uint)_indexCount, 1, 0, 0, 0);
        }
    }

    public struct WorldAndInverse
    {
        public Matrix4x4 World;
        public Matrix4x4 InverseWorld;
    }
}
