﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using VoxelPizza.Diagnostics;

namespace VoxelPizza.Client
{
    public class SceneContext
    {
        private Camera? _currentCamera;

        public Profiler? Profiler { get; set; }

        public DeviceBuffer LightInfoBuffer { get; private set; }
        public DeviceBuffer LightViewProjectionBuffer0 { get; internal set; }
        public DeviceBuffer LightViewProjectionBuffer1 { get; internal set; }
        public DeviceBuffer LightViewProjectionBuffer2 { get; internal set; }
        public DeviceBuffer DepthLimitsBuffer { get; internal set; }
        public DeviceBuffer PointLightsBuffer { get; private set; }

        public CascadedShadowMaps ShadowMaps { get; private set; } = new CascadedShadowMaps();
        public TextureView NearShadowMapView => ShadowMaps.NearShadowMapView;
        public TextureView MidShadowMapView => ShadowMaps.MidShadowMapView;
        public TextureView FarShadowMapView => ShadowMaps.FarShadowMapView;
        public Framebuffer NearShadowMapFramebuffer => ShadowMaps.NearShadowMapFramebuffer;
        public Framebuffer MidShadowMapFramebuffer => ShadowMaps.MidShadowMapFramebuffer;
        public Framebuffer FarShadowMapFramebuffer => ShadowMaps.FarShadowMapFramebuffer;
        public Texture ShadowMapTexture => ShadowMaps.NearShadowMap; // Only used for size.

        // MainSceneView and Duplicator resource sets both use this.
        public ResourceLayout TextureSamplerResourceLayout { get; private set; }
        public ResourceLayout CameraInfoLayout { get; private set; }

        public Texture MainSceneColorTexture { get; private set; }
        public Texture MainSceneDepthTexture { get; private set; }
        public Framebuffer MainSceneFramebuffer { get; private set; }

        public Texture MainSceneResolvedColorTexture { get; private set; }
        public TextureView MainSceneResolvedColorView { get; private set; }
        public ResourceSet MainSceneViewResourceSet { get; private set; }

        public Texture DuplicatorTarget0 { get; private set; }
        public TextureView DuplicatorTargetView0 { get; private set; }
        public ResourceSet DuplicatorTargetSet0 { get; internal set; }
        public Texture DuplicatorTarget1 { get; private set; }
        public TextureView DuplicatorTargetView1 { get; private set; }
        public ResourceSet DuplicatorTargetSet1 { get; internal set; }
        public Framebuffer DuplicatorFramebuffer { get; private set; }

        public DirectionalLight DirectionalLight { get; } = new DirectionalLight();
        public TextureSampleCount MainSceneSampleCount { get; internal set; }

        public List<Camera> Cameras { get; } = new();
        public Dictionary<Camera, ResourceSet> CameraInfoSets { get; } = new();
        public Dictionary<Camera, DeviceBuffer> CameraInfoBuffers { get; } = new();

        public event Action<Camera?>? CameraChanged;

        public Camera? CurrentCamera
        {
            get => _currentCamera; 
            set
            {
                if (value != null)
                {
                    if (!Cameras.Contains(value))
                    {
                        throw new InvalidOperationException("The camera has not been registered.");
                    }
                }
                CameraChanged?.Invoke(value);
                _currentCamera = value;
            }
        }

        public virtual void CreateGraphicsDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;
            LightViewProjectionBuffer0 = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));
            LightViewProjectionBuffer0.Name = "LightViewProjectionBuffer0";
            LightViewProjectionBuffer1 = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));
            LightViewProjectionBuffer1.Name = "LightViewProjectionBuffer1";
            LightViewProjectionBuffer2 = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));
            LightViewProjectionBuffer2.Name = "LightViewProjectionBuffer2";
            DepthLimitsBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<DepthCascadeLimits>(), BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));
            LightInfoBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<DirectionalLightInfo>(), BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));
            PointLightsBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<PointLightsInfo.Blittable>(), BufferUsage.UniformBuffer));
            PointLightsInfo pli = new PointLightsInfo();
            pli.PointLights = new PointLightInfo[4]
            {
                new PointLightInfo { Color = new Vector3(.6f, .6f, .6f), Position = new Vector3(-50, 5, 0), Range = 75f },
                new PointLightInfo { Color = new Vector3(.6f, .35f, .4f), Position = new Vector3(0, 5, 0), Range = 100f },
                new PointLightInfo { Color = new Vector3(.6f, .6f, 0.35f), Position = new Vector3(50, 5, 0), Range = 40f },
                new PointLightInfo { Color = new Vector3(0.4f, 0.4f, .6f), Position = new Vector3(25, 5, 45), Range = 150f },
            };
            pli.NumActiveLights = pli.PointLights.Length;

            cl.UpdateBuffer(PointLightsBuffer, 0, pli.GetBlittable());

            TextureSamplerResourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("SourceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            CameraInfoLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("CameraInfo", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)));

            RecreateWindowSizedResources(gd, cl);

            ShadowMaps.CreateDeviceResources(gd);

            CreateCameraDeviceObjects(gd);
        }

        private void CreateCameraDeviceObjects(GraphicsDevice gd)
        {
            ResourceFactory factory = gd.ResourceFactory;

            foreach (Camera camera in Cameras)
            {
                var cameraInfoBuffer = factory.CreateBuffer(new BufferDescription(
                    (uint)Unsafe.SizeOf<CameraInfo>(), BufferUsage.UniformBuffer | BufferUsage.DynamicWrite));

                var cameraInfoSet = factory.CreateResourceSet(new ResourceSetDescription(
                    CameraInfoLayout, cameraInfoBuffer));

                CameraInfoSets.Add(camera, cameraInfoSet);
                CameraInfoBuffers.Add(camera, cameraInfoBuffer);
            }
        }

        public virtual void DisposeGraphicsDeviceObjects()
        {
            LightInfoBuffer.Dispose();
            LightViewProjectionBuffer0.Dispose();
            LightViewProjectionBuffer1.Dispose();
            LightViewProjectionBuffer2.Dispose();
            DepthLimitsBuffer.Dispose();
            PointLightsBuffer.Dispose();
            MainSceneColorTexture.Dispose();
            MainSceneResolvedColorTexture.Dispose();
            MainSceneResolvedColorView.Dispose();
            MainSceneDepthTexture.Dispose();
            MainSceneFramebuffer.Dispose();
            MainSceneViewResourceSet.Dispose();
            DuplicatorTarget0.Dispose();
            DuplicatorTarget1.Dispose();
            DuplicatorTargetView0.Dispose();
            DuplicatorTargetView1.Dispose();
            DuplicatorTargetSet0.Dispose();
            DuplicatorTargetSet1.Dispose();
            DuplicatorFramebuffer.Dispose();
            TextureSamplerResourceLayout.Dispose();
            ShadowMaps.DestroyDeviceObjects();

            DestoryCameraDeviceObjects();
        }

        private void DestoryCameraDeviceObjects()
        {
            foreach (var cameraInfoSet in CameraInfoSets)
            {
                cameraInfoSet.Value.Dispose();
            }
            CameraInfoSets.Clear();

            foreach (var cameraInfoBuffer in CameraInfoBuffers)
            {
                cameraInfoBuffer.Value.Dispose();
            }
            CameraInfoBuffers.Clear();

            CameraInfoLayout.Dispose();
        }

        public void AddCamera(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            Cameras.Add(camera);
        }

        public ResourceSet GetCameraInfoSet(Camera camera)
        {
            return CameraInfoSets[camera];
        }

        public void UpdateCameraBuffers(CommandList cl)
        {
            foreach ((Camera camera, DeviceBuffer buffer) in CameraInfoBuffers)
            {
                cl.UpdateBuffer(buffer, 0, camera.GetCameraInfo());
            }
        }

        internal void RecreateWindowSizedResources(GraphicsDevice gd, CommandList cl)
        {
            MainSceneColorTexture?.Dispose();
            MainSceneDepthTexture?.Dispose();
            MainSceneResolvedColorTexture?.Dispose();
            MainSceneResolvedColorView?.Dispose();
            MainSceneViewResourceSet?.Dispose();
            MainSceneFramebuffer?.Dispose();
            DuplicatorTarget0?.Dispose();
            DuplicatorTarget1?.Dispose();
            DuplicatorTargetView0?.Dispose();
            DuplicatorTargetView1?.Dispose();
            DuplicatorTargetSet0?.Dispose();
            DuplicatorTargetSet1?.Dispose();
            DuplicatorFramebuffer?.Dispose();

            ResourceFactory factory = gd.ResourceFactory;

            gd.GetPixelFormatSupport(
                PixelFormat.R16_G16_B16_A16_Float,
                TextureType.Texture2D,
                TextureUsage.RenderTarget,
                out PixelFormatProperties properties);

            TextureSampleCount sampleCount = MainSceneSampleCount;
            while (!properties.IsSampleCountSupported(sampleCount))
            {
                sampleCount -= 1;
            }

            TextureDescription mainColorDesc = TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R16_G16_B16_A16_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled,
                sampleCount);

            MainSceneColorTexture = factory.CreateTexture(mainColorDesc);
            if (sampleCount != TextureSampleCount.Count1)
            {
                mainColorDesc.SampleCount = TextureSampleCount.Count1;
                MainSceneResolvedColorTexture = factory.CreateTexture(mainColorDesc);
            }
            else
            {
                MainSceneResolvedColorTexture = MainSceneColorTexture;
            }
            MainSceneResolvedColorView = factory.CreateTextureView(MainSceneResolvedColorTexture);
            MainSceneDepthTexture = factory.CreateTexture(TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.DepthStencil,
                sampleCount));
            MainSceneFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(MainSceneDepthTexture, MainSceneColorTexture));
            MainSceneViewResourceSet = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, MainSceneResolvedColorView, gd.PointSampler));

            TextureDescription colorTargetDesc = TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R16_G16_B16_A16_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled);
            DuplicatorTarget0 = factory.CreateTexture(colorTargetDesc);
            DuplicatorTargetView0 = factory.CreateTextureView(DuplicatorTarget0);
            DuplicatorTarget1 = factory.CreateTexture(colorTargetDesc);
            DuplicatorTargetView1 = factory.CreateTextureView(DuplicatorTarget1);
            DuplicatorTargetSet0 = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, DuplicatorTargetView0, gd.PointSampler));
            DuplicatorTargetSet1 = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, DuplicatorTargetView1, gd.PointSampler));

            FramebufferDescription fbDesc = new FramebufferDescription(null, DuplicatorTarget0, DuplicatorTarget1);
            DuplicatorFramebuffer = factory.CreateFramebuffer(fbDesc);
        }
    }

    public class CascadedShadowMaps
    {
        public Texture NearShadowMap { get; private set; }
        public TextureView NearShadowMapView { get; private set; }
        public Framebuffer NearShadowMapFramebuffer { get; private set; }

        public Texture MidShadowMap { get; private set; }
        public TextureView MidShadowMapView { get; private set; }
        public Framebuffer MidShadowMapFramebuffer { get; private set; }

        public Texture FarShadowMap { get; private set; }
        public TextureView FarShadowMapView { get; private set; }
        public Framebuffer FarShadowMapFramebuffer { get; private set; }

        public void CreateDeviceResources(GraphicsDevice gd)
        {
            var factory = gd.ResourceFactory;
            TextureDescription desc = TextureDescription.Texture2D(
                2048, 2048, 1, 1, PixelFormat.D32_Float_S8_UInt, TextureUsage.DepthStencil | TextureUsage.Sampled);

            NearShadowMap = factory.CreateTexture(desc);
            NearShadowMap.Name = "Near Shadow Map";
            NearShadowMapView = factory.CreateTextureView(NearShadowMap);
            NearShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(NearShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));

            MidShadowMap = factory.CreateTexture(desc);
            MidShadowMapView = factory.CreateTextureView(new TextureViewDescription(MidShadowMap, 0, 1, 0, 1));
            MidShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(MidShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));

            FarShadowMap = factory.CreateTexture(desc);
            FarShadowMapView = factory.CreateTextureView(new TextureViewDescription(FarShadowMap, 0, 1, 0, 1));
            FarShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(FarShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));
        }

        public void DestroyDeviceObjects()
        {
            NearShadowMap.Dispose();
            NearShadowMapView.Dispose();
            NearShadowMapFramebuffer.Dispose();

            MidShadowMap.Dispose();
            MidShadowMapView.Dispose();
            MidShadowMapFramebuffer.Dispose();

            FarShadowMap.Dispose();
            FarShadowMapView.Dispose();
            FarShadowMapFramebuffer.Dispose();
        }
    }
}
