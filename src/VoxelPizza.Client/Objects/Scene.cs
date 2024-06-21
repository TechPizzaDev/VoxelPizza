using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.Utilities;
using VoxelPizza.Diagnostics;

namespace VoxelPizza.Client
{
    public class Scene
    {
        private readonly Octree<CullRenderable> _octree = new(new BoundingBox(Vector3.One * -50, Vector3.One * 50), 2);

        private readonly List<GraphicsResource> _graphicsResources = new();
        private readonly List<Renderable> _freeRenderables = new();
        private readonly List<IUpdateable> _updateables = new();

        private readonly ConcurrentDictionary<RenderPasses, Func<CullRenderable, bool>> _filters = new();

        public Camera PrimaryCamera { get; }
        public Camera SecondaryCamera { get; }

        public bool ThreadedRendering { get; set; } = false;

        private float _lScale = 1f;
        private float _rScale = 1f;
        private float _tScale = 1f;
        private float _bScale = 1f;
        private float _nScale = 4f;
        private float _fScale = 4f;

        private float _nearCascadeLimit = 100;
        private float _midCascadeLimit = 300;

        public Scene(GraphicsDevice gd, Sdl2Window window)
        {
            PrimaryCamera = new Camera(gd, window);
            SecondaryCamera = new Camera(gd, window);
        }

        public void AddGraphicsResource(GraphicsResource graphicsResource)
        {
            if (graphicsResource == null)
                throw new ArgumentNullException(nameof(graphicsResource));

            _graphicsResources.Add(graphicsResource);
        }

        public void RemoveGraphicsResource(GraphicsResource graphicsResource)
        {
            if (graphicsResource == null)
                throw new ArgumentNullException(nameof(graphicsResource));

            _graphicsResources.Remove(graphicsResource);
        }

        public void AddRenderable(Renderable renderable, bool addAsGraphicsResource = true)
        {
            if (renderable == null)
                throw new ArgumentNullException(nameof(renderable));

            if (renderable is CullRenderable cr)
            {
                _octree.AddItem(cr.BoundingBox, cr);
            }
            else
            {
                _freeRenderables.Add(renderable);
            }

            if (addAsGraphicsResource)
                AddGraphicsResource(renderable);
        }

        public void AddUpdateable(IUpdateable updateable)
        {
            if (updateable == null)
                throw new ArgumentNullException(nameof(updateable));

            _updateables.Add(updateable);
        }

        public void RemoveRenderable(Renderable renderable, bool removeGraphicsResource = true)
        {
            if (renderable == null)
                throw new ArgumentNullException(nameof(renderable));

            if (renderable is CullRenderable cr)
            {
                _octree.RemoveItem(cr);
            }
            else
            {
                _freeRenderables.Remove(renderable);
            }

            if (removeGraphicsResource)
                RemoveGraphicsResource(renderable);
        }

        public void RemoveUpdateable(IUpdateable updateable)
        {
            if (updateable == null)
                throw new ArgumentNullException(nameof(updateable));

            _updateables.Remove(updateable);
        }

        public void Update(in UpdateState state, SceneContext sc)
        {
            foreach (IUpdateable updateable in _updateables)
            {
                updateable.Update(state);
            }

            sc.CurrentCamera?.Update(state);
        }

        private readonly Task[] _renderTasks = new Task[4];

        public void RenderAllStages(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            if (ThreadedRendering)
            {
                RenderAllMultiThreaded(gd, cl, sc);
            }
            else
            {
                RenderAllSingleThread(gd, cl, sc);
            }
        }

        private void RenderAllSingleThread(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            Camera? camera = sc.CurrentCamera;
            if (camera == null)
            {
                return;
            }

            RenderQueue renderQueue = _renderQueues[0];
            List<CullRenderable> cullableStage = _cullableStage[0];
            List<Renderable> renderableStage = _renderableStage[0];

            float depthClear = gd.IsDepthRangeZeroToOne ? 0f : 1f;
            Matrix4x4 cameraProj = camera.ProjectionMatrix;
            Vector4 nearLimitCS = Vector4.Transform(new Vector3(0, 0, -_nearCascadeLimit), cameraProj);
            Vector4 midLimitCS = Vector4.Transform(new Vector3(0, 0, -_midCascadeLimit), cameraProj);
            Vector4 farLimitCS = Vector4.Transform(new Vector3(0, 0, -camera.FarDistance), cameraProj);

            Vector3 lightPos = sc.DirectionalLight.Transform.Position - sc.DirectionalLight.Direction * 1000f;
            // Near
            cl.PushDebugGroup("Shadow Map - Near Cascade");
            Matrix4x4 viewProj0 = UpdateDirectionalLightMatrices(
                gd,
                sc,
                camera.NearDistance,
                _nearCascadeLimit,
                sc.ShadowMapTexture.Width,
                out BoundingFrustum lightFrustum);
            cl.UpdateBuffer(sc.LightViewProjectionBuffer0, 0, ref viewProj0);
            cl.SetFramebuffer(sc.NearShadowMapFramebuffer);
            cl.SetFullViewports();
            cl.ClearDepthStencil(depthClear);
            Render(gd, cl, sc, RenderPasses.ShadowMapNear, lightFrustum, lightPos, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            // Mid
            cl.PushDebugGroup("Shadow Map - Mid Cascade");
            Matrix4x4 viewProj1 = UpdateDirectionalLightMatrices(
                gd,
                sc,
                _nearCascadeLimit,
                _midCascadeLimit,
                sc.ShadowMapTexture.Width,
                out lightFrustum);
            cl.UpdateBuffer(sc.LightViewProjectionBuffer1, 0, ref viewProj1);
            cl.SetFramebuffer(sc.MidShadowMapFramebuffer);
            cl.SetFullViewports();
            cl.ClearDepthStencil(depthClear);
            Render(gd, cl, sc, RenderPasses.ShadowMapMid, lightFrustum, lightPos, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            //// Far
            //cl.PushDebugGroup("Shadow Map - Far Cascade");
            //Matrix4x4 viewProj2 = UpdateDirectionalLightMatrices(
            //    gd,
            //    sc,
            //    _midCascadeLimit,
            //    _farCascadeLimit,
            //    sc.ShadowMapTexture.Width,
            //    out lightFrustum);
            //cl.UpdateBuffer(sc.LightViewProjectionBuffer2, 0, ref viewProj2);
            //cl.SetFramebuffer(sc.FarShadowMapFramebuffer);
            //cl.SetFullViewports();
            //cl.ClearDepthStencil(depthClear);
            //Render(gd, cl, sc, RenderPasses.ShadowMapFar, lightFrustum, lightPos, renderQueue, cullableStage, renderableStage, null, false);
            //cl.PopDebugGroup();

            // Main scene

            cl.PushDebugGroup("Main Scene Pass");
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            float fbWidth = sc.MainSceneFramebuffer.Width;
            float fbHeight = sc.MainSceneFramebuffer.Height;
            cl.SetViewport(0, new Viewport(0, 0, fbWidth, fbHeight, 0, 1f));
            cl.ClearDepthStencil(depthClear);
            cl.SetFullScissorRects();
            sc.UpdateCameraBuffers(cl); // Re-set because reflection step changed it.
            BoundingFrustum cameraFrustum = new(camera.ViewMatrix * camera.ProjectionMatrix);

            Render(gd, cl, sc, RenderPasses.Opaque, cameraFrustum, camera.Position, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            cl.PushDebugGroup("Transparent Pass");
            Render(gd, cl, sc, RenderPasses.AlphaBlend, cameraFrustum, camera.Position, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            cl.PushDebugGroup("Overlay");
            Render(gd, cl, sc, RenderPasses.Overlay, cameraFrustum, camera.Position, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            if (sc.MainSceneColorTexture.SampleCount != TextureSampleCount.Count1)
            {
                cl.ResolveTexture(sc.MainSceneColorTexture, sc.MainSceneResolvedColorTexture);
            }

            cl.PushDebugGroup("Duplicator");
            cl.SetFramebuffer(sc.DuplicatorFramebuffer);
            cl.SetFullViewports();
            Render(gd, cl, sc, RenderPasses.Duplicator, new BoundingFrustum(), camera.Position, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            cl.PushDebugGroup("Swapchain Pass");
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.SetFullViewports();
            Render(gd, cl, sc, RenderPasses.SwapchainOutput, new BoundingFrustum(), camera.Position, renderQueue, cullableStage, renderableStage, null, false);
            cl.PopDebugGroup();

            _resourceUpdateCL.Begin();
            {
                _resourceUpdateCL.UpdateBuffer(sc.DepthLimitsBuffer, 0, new DepthCascadeLimits
                {
                    NearLimit = nearLimitCS.Z,
                    MidLimit = midLimitCS.Z,
                    FarLimit = farLimitCS.Z
                });

                _resourceUpdateCL.UpdateBuffer(sc.LightInfoBuffer, 0, sc.DirectionalLight.GetInfo());

                foreach (Renderable renderable in _allPerFrameRenderablesSet)
                {
                    renderable.UpdatePerFrameResources(gd, _resourceUpdateCL, sc);
                }
            }
            _resourceUpdateCL.End();
            gd.SubmitCommands(_resourceUpdateCL);
        }

        private CommandList[] _multithreadCls;

        private void RenderAllMultiThreaded(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            using ProfilerPopToken profilerToken = sc.Profiler.Push();

            Camera? camera = sc.CurrentCamera;
            if (camera == null)
            {
                return;
            }

            float depthClear = gd.IsDepthRangeZeroToOne ? 0f : 1f;
            Matrix4x4 cameraProj = camera.ProjectionMatrix;
            Vector4 nearLimitCS = Vector4.Transform(new Vector3(0, 0, -_nearCascadeLimit), cameraProj);
            Vector4 midLimitCS = Vector4.Transform(new Vector3(0, 0, -_midCascadeLimit), cameraProj);
            Vector4 farLimitCS = Vector4.Transform(new Vector3(0, 0, -camera.FarDistance), cameraProj);
            Vector3 lightPos = sc.DirectionalLight.Transform.Position - sc.DirectionalLight.Direction * 1000f;

            CommandList[] cls = _multithreadCls;
            for (int i = 0; i < cls.Length; i++)
                cls[i].Begin();

            _allPerFrameRenderablesSet.Clear();
            _renderTasks[0] = Task.Run(() =>
            {
                // Near
                Matrix4x4 viewProj0 = UpdateDirectionalLightMatrices(
                    gd,
                    sc,
                    camera.NearDistance,
                    _nearCascadeLimit,
                    sc.ShadowMapTexture.Width,
                    out BoundingFrustum lightFrustum0);
                cls[0].UpdateBuffer(sc.LightViewProjectionBuffer0, 0, ref viewProj0);

                cls[0].SetFramebuffer(sc.NearShadowMapFramebuffer);
                cls[0].SetViewport(0, new Viewport(0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height, 0, 1));
                cls[0].SetScissorRect(0, 0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height);
                cls[0].ClearDepthStencil(depthClear);
                Render(gd, cls[0], sc, RenderPasses.ShadowMapNear, lightFrustum0, lightPos, _renderQueues[0], _cullableStage[0], _renderableStage[0], null, true);
            });

            _renderTasks[1] = Task.Run(() =>
            {
                // Mid
                Matrix4x4 viewProj1 = UpdateDirectionalLightMatrices(
                    gd,
                    sc,
                    _nearCascadeLimit,
                    _midCascadeLimit,
                    sc.ShadowMapTexture.Width,
                    out BoundingFrustum lightFrustum1);
                cls[1].UpdateBuffer(sc.LightViewProjectionBuffer1, 0, ref viewProj1);

                cls[1].SetFramebuffer(sc.MidShadowMapFramebuffer);
                cls[1].SetViewport(0, new Viewport(0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height, 0, 1));
                cls[1].SetScissorRect(0, 0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height);
                cls[1].ClearDepthStencil(depthClear);
                Render(gd, cls[1], sc, RenderPasses.ShadowMapMid, lightFrustum1, lightPos, _renderQueues[1], _cullableStage[1], _renderableStage[1], null, true);
            });

            _renderTasks[2] = Task.Run(() =>
            {
                // Far
                Matrix4x4 viewProj2 = UpdateDirectionalLightMatrices(
                    gd,
                    sc,
                    _midCascadeLimit,
                    camera.FarDistance,
                    sc.ShadowMapTexture.Width,
                    out BoundingFrustum lightFrustum2);
                cls[2].UpdateBuffer(sc.LightViewProjectionBuffer2, 0, ref viewProj2);

                cls[2].SetFramebuffer(sc.FarShadowMapFramebuffer);
                cls[2].SetViewport(0, new Viewport(0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height, 0, 1));
                cls[2].SetScissorRect(0, 0, 0, sc.ShadowMapTexture.Width, sc.ShadowMapTexture.Height);
                cls[2].ClearDepthStencil(depthClear);
                Render(gd, cls[2], sc, RenderPasses.ShadowMapFar, lightFrustum2, lightPos, _renderQueues[2], _cullableStage[2], _renderableStage[2], null, true);
            });

            _renderTasks[3] = Task.Run(() =>
            {
                // Main scene
                cls[3].SetFramebuffer(sc.MainSceneFramebuffer);
                float scWidth = sc.MainSceneFramebuffer.Width;
                float scHeight = sc.MainSceneFramebuffer.Height;
                cls[3].SetViewport(0, new Viewport(0, 0, scWidth, scHeight, 0, 1f));
                cls[3].SetScissorRect(0, 0, 0, (uint)scWidth, (uint)scHeight);
                cls[3].ClearDepthStencil(depthClear);
                sc.UpdateCameraBuffers(cls[3]);
                BoundingFrustum cameraFrustum = new(camera.ViewMatrix * camera.ProjectionMatrix);

                Render(gd, cls[3], sc, RenderPasses.Opaque, cameraFrustum, camera.Position, _renderQueues[3], _cullableStage[3], _renderableStage[3], null, true);
                Render(gd, cls[3], sc, RenderPasses.AlphaBlend, cameraFrustum, camera.Position, _renderQueues[3], _cullableStage[3], _renderableStage[3], null, true);
                Render(gd, cls[3], sc, RenderPasses.Overlay, cameraFrustum, camera.Position, _renderQueues[3], _cullableStage[3], _renderableStage[3], null, true);
            });

            Task.WaitAll(_renderTasks);

            for (int i = 0; i < cls.Length; i++)
            {
                cls[i].End();
                gd.SubmitCommands(cls[i]);
            }

            if (sc.MainSceneColorTexture.SampleCount != TextureSampleCount.Count1)
            {
                cl.ResolveTexture(sc.MainSceneColorTexture, sc.MainSceneResolvedColorTexture);
            }

            cl.SetFramebuffer(sc.DuplicatorFramebuffer);
            uint fbWidth = sc.DuplicatorFramebuffer.Width;
            uint fbHeight = sc.DuplicatorFramebuffer.Height;
            cl.SetViewport(0, new Viewport(0, 0, fbWidth, fbHeight, 0, 1));
            cl.SetViewport(1, new Viewport(0, 0, fbWidth, fbHeight, 0, 1));
            cl.SetScissorRect(0, 0, 0, fbWidth, fbHeight);
            cl.SetScissorRect(1, 0, 0, fbWidth, fbHeight);
            Render(gd, cl, sc, RenderPasses.Duplicator, new BoundingFrustum(), camera.Position, _renderQueues[0], _cullableStage[0], _renderableStage[0], null, false);

            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            fbWidth = gd.SwapchainFramebuffer.Width;
            fbHeight = gd.SwapchainFramebuffer.Height;
            cl.SetViewport(0, new Viewport(0, 0, fbWidth, fbHeight, 0, 1));
            cl.SetScissorRect(0, 0, 0, fbWidth, fbHeight);
            Render(gd, cl, sc, RenderPasses.SwapchainOutput, new BoundingFrustum(), camera.Position, _renderQueues[0], _cullableStage[0], _renderableStage[0], null, false);

            _resourceUpdateCL.Begin();
            {
                _resourceUpdateCL.UpdateBuffer(sc.DepthLimitsBuffer, 0, new DepthCascadeLimits
                {
                    NearLimit = nearLimitCS.Z,
                    MidLimit = midLimitCS.Z,
                    FarLimit = farLimitCS.Z
                });

                _resourceUpdateCL.UpdateBuffer(sc.LightInfoBuffer, 0, sc.DirectionalLight.GetInfo());

                foreach (Renderable renderable in _allPerFrameRenderablesSet)
                {
                    renderable.UpdatePerFrameResources(gd, _resourceUpdateCL, sc);
                }
            }
            _resourceUpdateCL.End();
            gd.SubmitCommands(_resourceUpdateCL);
        }

        private Matrix4x4 UpdateDirectionalLightMatrices(
            GraphicsDevice gd,
            SceneContext sc,
            float near,
            float far,
            uint shadowMapWidth,
            out BoundingFrustum lightFrustum)
        {
            Camera? camera = sc.CurrentCamera;
            if (camera == null)
            {
                lightFrustum = default;
                return Matrix4x4.Identity;
            }

            Vector3 lightDir = sc.DirectionalLight.Direction;
            Vector3 viewDir = camera.LookDirection;
            Vector3 viewPos = camera.Position;
            Vector3 unitY = Vector3.UnitY;
            FrustumCorners cameraCorners;

            if (gd.IsDepthRangeZeroToOne)
            {
                FrustumHelpers.ComputePerspectiveFrustumCorners(
                    viewPos,
                    viewDir,
                    unitY,
                    camera.FieldOfView,
                    far,
                    near,
                    camera.AspectRatio,
                    out cameraCorners);
            }
            else
            {
                FrustumHelpers.ComputePerspectiveFrustumCorners(
                    viewPos,
                    viewDir,
                    unitY,
                    camera.FieldOfView,
                    near,
                    far,
                    camera.AspectRatio,
                    out cameraCorners);
            }

            // Approach used: http://alextardif.com/ShadowMapping.html

            Vector3 frustumCenter = Vector3.Zero;
            frustumCenter += cameraCorners.NearTopLeft;
            frustumCenter += cameraCorners.NearTopRight;
            frustumCenter += cameraCorners.NearBottomLeft;
            frustumCenter += cameraCorners.NearBottomRight;
            frustumCenter += cameraCorners.FarTopLeft;
            frustumCenter += cameraCorners.FarTopRight;
            frustumCenter += cameraCorners.FarBottomLeft;
            frustumCenter += cameraCorners.FarBottomRight;
            frustumCenter /= 8f;

            float radius = (cameraCorners.NearTopLeft - cameraCorners.FarBottomRight).Length() / 2.0f;
            float texelsPerUnit = shadowMapWidth / (radius * 2.0f);

            Matrix4x4 scalar = Matrix4x4.CreateScale(texelsPerUnit, texelsPerUnit, texelsPerUnit);

            Vector3 baseLookAt = -lightDir;

            Matrix4x4 lookat = Matrix4x4.CreateLookAt(Vector3.Zero, baseLookAt, Vector3.UnitY);
            lookat = scalar * lookat;
            Matrix4x4.Invert(lookat, out Matrix4x4 lookatInv);

            frustumCenter = Vector3.Transform(frustumCenter, lookat);
            frustumCenter.X = (int)frustumCenter.X;
            frustumCenter.Y = (int)frustumCenter.Y;
            frustumCenter = Vector3.Transform(frustumCenter, lookatInv);

            Vector3 lightPos = frustumCenter - (lightDir * radius * 2f);

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, frustumCenter, Vector3.UnitY);

            Matrix4x4 lightProjection = Util.CreateOrtho(
                gd,
                gd.IsDepthRangeZeroToOne,
                -radius * _lScale,
                radius * _rScale,
                -radius * _bScale,
                radius * _tScale,
                -radius * _nScale,
                radius * _fScale);

            Matrix4x4 viewProjectionMatrix = lightView * lightProjection;

            lightFrustum = new BoundingFrustum(viewProjectionMatrix);
            return viewProjectionMatrix;
        }

        public void Render(
            GraphicsDevice gd,
            CommandList rc,
            SceneContext sc,
            RenderPasses pass,
            BoundingFrustum frustum,
            Vector3 viewPosition,
            RenderQueue renderQueue,
            List<CullRenderable> cullRenderableList,
            List<Renderable> renderableList,
            Comparer<RenderItemIndex>? comparer,
            bool threaded)
        {
            renderQueue.Clear();

            cullRenderableList.Clear();
            CollectVisibleObjects(ref frustum, pass, cullRenderableList);
            renderQueue.AddRange(cullRenderableList, viewPosition);

            renderableList.Clear();
            CollectFreeObjects(pass, renderableList);
            renderQueue.AddRange(renderableList, viewPosition);

            if (comparer == null)
            {
                renderQueue.Sort();
            }
            else
            {
                renderQueue.Sort(comparer);
            }

            foreach (Renderable renderable in renderQueue)
            {
                renderable.Render(gd, rc, sc, pass);
            }

            if (threaded)
            {
                lock (_allPerFrameRenderablesSet)
                {
                    foreach (CullRenderable thing in cullRenderableList)
                        _allPerFrameRenderablesSet.Add(thing);
                    foreach (Renderable thing in renderableList)
                        _allPerFrameRenderablesSet.Add(thing);
                }
            }
            else
            {
                foreach (CullRenderable thing in cullRenderableList)
                    _allPerFrameRenderablesSet.Add(thing);
                foreach (Renderable thing in renderableList)
                    _allPerFrameRenderablesSet.Add(thing);
            }
        }

        private readonly HashSet<Renderable> _allPerFrameRenderablesSet = new();
        private readonly RenderQueue[] _renderQueues = Enumerable.Range(0, 4).Select(i => new RenderQueue()).ToArray();
        private readonly List<CullRenderable>[] _cullableStage = Enumerable.Range(0, 4).Select(i => new List<CullRenderable>()).ToArray();
        private readonly List<Renderable>[] _renderableStage = Enumerable.Range(0, 4).Select(i => new List<Renderable>()).ToArray();

        private void CollectVisibleObjects(
            ref BoundingFrustum frustum,
            RenderPasses renderPass,
            List<CullRenderable> renderables)
        {
            _octree.GetContainedObjects(frustum, renderables, GetFilter(renderPass));
        }

        private void CollectFreeObjects(RenderPasses renderPass, List<Renderable> renderables)
        {
            foreach (Renderable r in _freeRenderables)
            {
                if ((r.RenderPasses & renderPass) != 0)
                {
                    renderables.Add(r);
                }
            }
        }

        private static Func<RenderPasses, Func<CullRenderable, bool>> s_createFilterFunc = rp => CreateFilter(rp);
        private CommandList _resourceUpdateCL;

        private Func<CullRenderable, bool> GetFilter(RenderPasses passes)
        {
            return _filters.GetOrAdd(passes, s_createFilterFunc);
        }

        private static Func<CullRenderable, bool> CreateFilter(RenderPasses rp)
        {
            // This cannot be inlined into GetFilter -- a Roslyn bug causes copious allocations.
            // https://github.com/dotnet/roslyn/issues/22589
            return cr => (cr.RenderPasses & rp) == rp;
        }

        internal void DestroyGraphicsDeviceObjects()
        {
            for (int i = 0; i < _multithreadCls.Length; i++)
                _multithreadCls[i].Dispose();

            foreach (GraphicsResource resource in _graphicsResources)
                resource.DestroyDeviceObjects();

            _resourceUpdateCL.Dispose();
        }

        internal void CreateGraphicsDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            _multithreadCls = new CommandList[4];
            for (int i = 0; i < _multithreadCls.Length; i++)
                _multithreadCls[i] = gd.ResourceFactory.CreateCommandList();

            foreach (GraphicsResource resource in _graphicsResources)
                resource.CreateDeviceObjects(gd, cl, sc);

            _resourceUpdateCL = gd.ResourceFactory.CreateCommandList();
            _resourceUpdateCL.Name = "Scene Resource Update Command List";
        }

        private class RenderPassesComparer : IEqualityComparer<RenderPasses>
        {
            public bool Equals(RenderPasses x, RenderPasses y)
            {
                return x == y;
            }

            public int GetHashCode(RenderPasses obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
