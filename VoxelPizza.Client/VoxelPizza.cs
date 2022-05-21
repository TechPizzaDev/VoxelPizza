using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ImGuiNET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;
using Veldrid.Utilities;
using VoxelPizza.Client.Objects;
using VoxelPizza.Client.Rendering.Voxels;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class VoxelPizza : Application
    {
        private static RenderDoc? _renderDoc;

        private List<Profiler.FrameSet> _frameSets = new();

        private Sdl2ControllerTracker? _controllerTracker;

        private Scene _scene;
        private SceneContext _sc;

        private List<FencedCommandList> _commandLists = new();
        private List<FencedCommandList> _submittedCommandLists = new();

        private FullScreenQuad _fsq;
        private bool _controllerDebugMenu;

        private readonly string[] _msaaOptions = new string[] { "Off", "2x", "4x", "8x", "16x", "32x" };
        private int _msaaOption = 0;
        private TextureSampleCount? _newSampleCount;
        private GraphicsBackend? _newGraphicsBackend;

        private List<Task> _loadTasks = new();
        private ConcurrentQueue<Renderable> _queuedRenderables = new();
        private Dictionary<string, ImageSharpTexture> _textures = new();

        private event Action<int, int> _resizeHandled;
        private bool _windowResized = true;

        public AudioTest audioTest;
        private ParticlePlane? particlePlane;

        private WorldManager _worldManager;
        private Dimension _currentDimension;

        private RenderRegionManager _renderRegionManager;
        private RenderRegionRenderer _renderRegionRenderer;

        public ImGuiRenderable ImGuiRenderable { get; }
        public ChunkRenderer? ChunkRenderer { get; }
        public ChunkBorderRenderer ChunkBorderRenderer { get; }

        public VoxelPizza() : base(preferredBackend: GraphicsBackend.Vulkan)
        {
            Sdl2Native.SDL_Init(SDLInitFlags.GameController | SDLInitFlags.Audio);
            SDLAudioBindings.LoadFunctions();
            Sdl2ControllerTracker.CreateDefault(out _controllerTracker);

            audioTest = new AudioTest();
            audioTest.Run();

            GraphicsDevice.SyncToVerticalBlank = true;

            _sc = new SceneContext();
            _sc.Profiler = new Profiler();
            _sc.CameraChanged += Scene_CameraChanged;

            _scene = new Scene(GraphicsDevice, Window);

            _scene.PrimaryCamera.Controller = _controllerTracker;
            _sc.AddCamera(_scene.PrimaryCamera);

            _scene.SecondaryCamera.Controller = _controllerTracker;
            _sc.AddCamera(_scene.SecondaryCamera);

            _scene.PrimaryCamera.Position = new Vector3(-6, 64f, -0.43f);
            _scene.PrimaryCamera.Yaw = MathF.PI * 1.25f;
            _scene.PrimaryCamera.Pitch = 0;

            ImGuiRenderable = new ImGuiRenderable(Window.Width, Window.Height);
            _resizeHandled += (w, h) => ImGuiRenderable.WindowResized(w, h);
            _scene.AddRenderable(ImGuiRenderable);

            ShadowmapDrawer texDrawIndexeder = new(() => Window, () => _sc.NearShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder.OnWindowResized();
            texDrawIndexeder.Position = new Vector2(10, 25);
            //_scene.AddRenderable(texDrawIndexeder);

            ShadowmapDrawer texDrawIndexeder2 = new(() => Window, () => _sc.MidShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder2.OnWindowResized();
            texDrawIndexeder2.Position = new Vector2(20 + texDrawIndexeder2.Size.X, 25);
            //_scene.AddRenderable(texDrawIndexeder2);

            ShadowmapDrawer texDrawIndexeder3 = new(() => Window, () => _sc.FarShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder3.OnWindowResized();
            texDrawIndexeder3.Position = new Vector2(30 + (texDrawIndexeder3.Size.X * 2), 25);
            //_scene.AddRenderable(texDrawIndexeder3);

            ScreenDuplicator duplicator = new();
            _scene.AddRenderable(duplicator);

            _fsq = new FullScreenQuad();
            _scene.AddRenderable(_fsq);

            particlePlane = null; // new ParticlePlane(_scene.PrimaryCamera);
            //_scene.AddRenderable(particlePlane);

            _worldManager = new WorldManager();
            _currentDimension = _worldManager.CreateDimension();

            MemoryHeap chunkMeshHeap = NativeMemoryHeap.Instance;
            chunkMeshHeap = new HeapPool(chunkMeshHeap, 1024 * 64 * 4);
            //ChunkRenderer = new ChunkRenderer(_currentDimension, chunkMeshHeap, new Size3(4, 3, 4));
            //ChunkRenderer.CullCamera = _scene.PrimaryCamera;
            //_scene.AddUpdateable(ChunkRenderer);
            //_scene.AddRenderable(ChunkRenderer);

            _renderRegionManager = new RenderRegionManager(_currentDimension, chunkMeshHeap, new Size3(4, 3, 4));
            _scene.AddUpdateable(_renderRegionManager);

            _renderRegionRenderer = new RenderRegionRenderer(_renderRegionManager);
            _scene.AddUpdateable(_renderRegionRenderer);
            _scene.AddRenderable(_renderRegionRenderer);

            _renderRegionManager.RegionAdded += (region) => _renderRegionRenderer.AddRegion(region.Position);
            _renderRegionManager.RegionUpdated += (region) => _renderRegionRenderer.UpdateRegion(region.Position);
            _renderRegionManager.RegionRemoved += (region) => _renderRegionRenderer.RemoveRegion(region.Position);

            ChunkBorderRenderer = new ChunkBorderRenderer();
            ChunkBorderRenderer.RegisterDimension(_currentDimension);
            //ChunkBorderRenderer.RegisterChunkRenderer()
            _scene.AddUpdateable(ChunkBorderRenderer);
            _scene.AddRenderable(ChunkBorderRenderer);

            _worldManager.CreateTestWorld(_currentDimension, true);

            _loadTasks.Add(Task.Run(() =>
            {
                Skybox skybox = GetDefaultSkybox();
                skybox.PreloadTexture(_sc);
                AddRenderable(skybox);
            }));

            //_loadTasks.Add(Task.Run(() =>
            //{
            //    string dir = "Models/SponzaAtrium";
            //    string obj = "sponza.obj";
            //    string mtl = "sponza.mtl";
            //
            //    AddObjModel(dir, obj, mtl);
            //}));

            CreateGraphicsDeviceObjects();

            ImGui.StyleColorsClassic();

            _sc.DirectionalLight.AmbientColor = new RgbaFloat(0.1f, 0.1f, 0.1f, 1);

            _sc.CurrentCamera = _scene.PrimaryCamera;

            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        }

        private void Scene_CameraChanged(Camera? camera)
        {
            ChunkRenderer? renderer = ChunkRenderer;
            if (renderer != null)
            {
                renderer.RenderCamera = camera;
            }

            RenderRegionRenderer? renderRegionRenderer = _renderRegionRenderer;
            if (renderRegionRenderer != null)
            {
                renderRegionRenderer.RenderCamera = camera;
            }
        }

        protected override void WindowResized()
        {
            _windowResized = true;
        }

        private void ChangeMsaa(int msaaOption)
        {
            TextureSampleCount sampleCount = (TextureSampleCount)msaaOption;
            _newSampleCount = sampleCount;
        }

        protected override void DisposeGraphicsDeviceObjects()
        {
            FinishSubmittedCommandLists(force: true);
            foreach (FencedCommandList frameCL in _commandLists)
                frameCL.Dispose();
            _commandLists.Clear();

            StaticResourceCache.DisposeGraphicsDeviceObjects();

            _sc.DisposeGraphicsDeviceObjects();
            _scene.DestroyGraphicsDeviceObjects();
        }

        protected override void CreateGraphicsDeviceObjects()
        {
            using CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
            cl.Name = "Recreation Initialization Command List";
            cl.Begin();
            {
                _sc.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
                _scene.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
            }
            cl.End();
            GraphicsDevice.SubmitCommands(cl);

            _scene.PrimaryCamera.UpdateGraphicsBackend(GraphicsDevice, Window);
        }

        protected override bool RunBody(bool forceDraw)
        {
            Profiler? profiler = _sc.Profiler;
            profiler?.Start();
            try
            {
                return base.RunBody(forceDraw);
            }
            finally
            {
                if (profiler != null)
                {
                    profiler.Stop();

                    List<Profiler.FrameSet> sets = profiler._sets;
                    if (sets.Count > 0)
                    {
                        for (int i = 0; i < sets.Count; i++)
                        {
                            if (_frameSets.Count <= i)
                                _frameSets.Add(new Profiler.FrameSet());

                            _frameSets[i].Items.Clear();
                            _frameSets[i].Items.AddRange(sets[i].Items);
                            _frameSets[i].Offset = sets[i].Offset;
                        }
                    }

                    profiler.Clear();
                }
            }
        }

        public override void Update(in FrameTime time)
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            // Console.WriteLine(((HeapPool)ChunkRenderer.ChunkMeshHeap).AvailableBytes / 1024 + "kB");

            UpdateState updateState = new(time, _sc.Profiler);

            _currentDimension.Update();

            ImGuiRenderable.Update(updateState);

            UpdateScene(updateState);

            particlePlane?.Update(time);

            Camera? camera = _sc.CurrentCamera;
            if (camera != null)
            {
                Vector3 camPos = camera.Position;
                Vector3 camLook = camera.LookDirection;

                audioTest.soloud.set3dListenerPosition(camPos.X, camPos.Y, camPos.Z);
                audioTest.soloud.set3dListenerAt(camLook.X, camLook.Y, camLook.Z);
                audioTest.soloud.set3dListenerUp(0, 1, 0);
            }

            float x = MathF.Sin(time.TotalSeconds) * 20;
            x = 0;
            audioTest.soloud.set3dSourcePosition(audioTest.voicehandle, x, 0, 0);
            audioTest.soloud.update3dAudio();

            //var t = audioTest.soloud.getStreamPosition(audioTest.voicehandle);
            //audioTest.soloud.seek(audioTest.voicehandle, t + LoudPizza.Time.FromSeconds(0.01));

            //_sc.DirectionalLight.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(time.TotalSeconds));

            if (_scene.PrimaryCamera != null)
            {
                // TODO: remove this

                Vector3 cullCameraPos = _scene.PrimaryCamera.Position;
                _currentDimension.PlayerChunkPosition = new BlockPosition(
                    (int)MathF.Round(cullCameraPos.X),
                    (int)MathF.Round(cullCameraPos.Y),
                    (int)MathF.Round(cullCameraPos.Z)).ToChunk();
            }

            if (InputTracker.GetKeyDown(Key.F11))
            {
                ToggleFullscreenState();
            }
        }

        private void UpdateScene(in UpdateState state)
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            _scene.Update(state, _sc);
        }

        public override void Draw()
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            if (_newGraphicsBackend.HasValue)
            {
                ChangeGraphicsBackend(_newGraphicsBackend.GetValueOrDefault());
                _newGraphicsBackend = null;
            }

            if (_windowResized)
            {
                _windowResized = false;

                int width = Window.Width;
                int height = Window.Height;
                GraphicsDevice.ResizeMainWindow((uint)width, (uint)height);
                _scene.PrimaryCamera.WindowResized(width, height);
                _resizeHandled?.Invoke(width, height);

                using CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
                cl.Begin();
                _sc.RecreateWindowSizedResources(GraphicsDevice, cl);
                cl.End();
                GraphicsDevice.SubmitCommands(cl);
            }

            if (_newSampleCount != null)
            {
                _sc.MainSceneSampleCount = _newSampleCount.Value;
                _newSampleCount = null;

                DisposeGraphicsDeviceObjects();
                CreateGraphicsDeviceObjects();
            }

            DrawOverlay();

            FinishSubmittedCommandLists(force: false);

            FencedCommandList fcl = AcquireFrameCommandList();
            fcl.CommandList.Begin();
            {
                while (_queuedRenderables.TryDequeue(out Renderable? renderable))
                {
                    _scene.AddRenderable(renderable);
                    renderable.CreateDeviceObjects(GraphicsDevice, fcl.CommandList, _sc);
                }

                _scene.RenderAllStages(GraphicsDevice, fcl.CommandList, _sc);
            }
            fcl.CommandList.End();
            GraphicsDevice.SubmitCommands(fcl.CommandList, fcl.Fence);
            _submittedCommandLists.Add(fcl);
        }

        private void FinishSubmittedCommandLists(bool force)
        {
            List<FencedCommandList> submittedCommandLists = _submittedCommandLists;

            for (int i = submittedCommandLists.Count; i-- > 0;)
            {
                FencedCommandList fcl = submittedCommandLists[i];
                if (force || fcl.Fence.Signaled)
                {
                    GraphicsDevice.ResetFence(fcl.Fence);
                    submittedCommandLists.RemoveAt(i);
                }
            }
        }

        private FencedCommandList AcquireFrameCommandList()
        {
            List<FencedCommandList> commandLists = _commandLists;
            List<FencedCommandList> submittedCommandLists = _submittedCommandLists;

            for (int i = 0; i < commandLists.Count; i++)
            {
                FencedCommandList fcl = commandLists[i];
                if (!submittedCommandLists.Contains(fcl))
                {
                    return fcl;
                }
            }

            int fclName = commandLists.Count + 1;

            CommandList commandList = GraphicsDevice.ResourceFactory.CreateCommandList();
            commandList.Name = $"Frame CommandList {fclName}";

            Fence fence = GraphicsDevice.ResourceFactory.CreateFence(false);
            fence.Name = $"Frame Fence {fclName}";

            FencedCommandList newFcl = new(commandList, fence);
            commandLists.Add(newFcl);
            return newFcl;
        }

        public override void Present()
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            base.Present();
        }

        private void DrawOverlay()
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            DrawMainMenu();

            DrawProfiler(_frameSets);

            if (ImGui.Begin("ChunkRenderer control"))
            {
                ChunkRenderer? renderer = ChunkRenderer;
                if (renderer != null)
                {
                    if (ImGui.Button("Reupload regions"))
                    {
                        renderer.ReuploadRegions();
                    }

                    if (ImGui.Button("Rebuild chunks"))
                    {
                        renderer.RebuildChunks();
                    }
                }
                ImGui.End();
            }
        }

        private void DrawMainMenu()
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            if (ImGui.BeginMainMenuBar())
            {
                DrawSettingsMenu();
                DrawWindowMenu();
                DrawRenderMenu();
                DrawDebugMenu();
                DrawRenderDocMenu();
                DrawControllerDebugMenu();

                ImGui.Separator();

                DrawTickTimes();

                ImGui.EndMainMenuBar();
            }
        }

        private void DrawTickTimes()
        {
            const string popupName = "Tick Time Popup";

            ImGui.SetNextWindowContentSize(new Vector2(180, 0));
            if (ImGui.BeginPopup(popupName))
            {
                ImGui.Columns(2);
                ImGui.Text("Update");
                ImGui.Text("Draw");
                ImGui.Text("Present");

                ImGui.NextColumn();
                ImGui.Text((TimeAverager.AverageUpdateSeconds * 1000).ToString("#00.00 ms"));
                ImGui.Text((TimeAverager.AverageDrawSeconds * 1000).ToString("#00.00 ms"));
                ImGui.Text((TimeAverager.AveragePresentSeconds * 1000).ToString("#00.00 ms"));

                ImGui.EndPopup();
            }

            if (ImGui.Button(TimeAverager.AverageTicksPerSecond.ToString("#00 fps")))
                ImGui.OpenPopup(popupName);
        }

        private void DrawSettingsMenu()
        {
            GraphicsBackend currentBackend = GraphicsDevice.BackendType;

            if (ImGui.BeginMenu("Settings"))
            {
                if (ImGui.BeginMenu("Graphics Backend"))
                {
                    if (ImGui.MenuItem("Vulkan", string.Empty, currentBackend == GraphicsBackend.Vulkan, GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan)))
                    {
                        _newGraphicsBackend = GraphicsBackend.Vulkan;
                    }
                    if (ImGui.MenuItem("OpenGL", string.Empty, currentBackend == GraphicsBackend.OpenGL, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGL)))
                    {
                        _newGraphicsBackend = GraphicsBackend.OpenGL;
                    }
                    if (ImGui.MenuItem("OpenGL ES", string.Empty, currentBackend == GraphicsBackend.OpenGLES, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGLES)))
                    {
                        _newGraphicsBackend = GraphicsBackend.OpenGLES;
                    }
                    if (ImGui.MenuItem("Direct3D 11", string.Empty, currentBackend == GraphicsBackend.Direct3D11, GraphicsDevice.IsBackendSupported(GraphicsBackend.Direct3D11)))
                    {
                        _newGraphicsBackend = GraphicsBackend.Direct3D11;
                    }
                    if (ImGui.MenuItem("Metal", string.Empty, currentBackend == GraphicsBackend.Metal, GraphicsDevice.IsBackendSupported(GraphicsBackend.Metal)))
                    {
                        _newGraphicsBackend = GraphicsBackend.Metal;
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("MSAA"))
                {
                    if (ImGui.Combo("MSAA", ref _msaaOption, _msaaOptions, _msaaOptions.Length))
                    {
                        ChangeMsaa(_msaaOption);
                    }

                    ImGui.EndMenu();
                }

                bool threadedRendering = _scene.ThreadedRendering;
                if (ImGui.MenuItem("Render with multiple threads", string.Empty, threadedRendering, true))
                {
                    _scene.ThreadedRendering = !_scene.ThreadedRendering;
                }

                bool tinted = _fsq.UseTintedTexture;
                if (ImGui.MenuItem("Tinted output", string.Empty, tinted, true))
                {
                    _fsq.UseTintedTexture = !tinted;
                }

                ImGui.EndMenu();
            }
        }

        private void DrawWindowMenu()
        {
            if (ImGui.BeginMenu("Window"))
            {
                bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
                if (ImGui.MenuItem("Fullscreen", "F11", isFullscreen, true))
                {
                    ToggleFullscreenState();
                }

                if (ImGui.MenuItem("Always Recreate Sdl2Window", string.Empty, AlwaysRecreateWindow, true))
                {
                    AlwaysRecreateWindow = !AlwaysRecreateWindow;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Causes a new OS window to be created whenever the graphics backend is switched. This is much safer, and is the default.");
                }

                if (ImGui.MenuItem("sRGB Swapchain Format", string.Empty, SrgbSwapchain, true))
                {
                    SrgbSwapchain = !SrgbSwapchain;
                    ChangeGraphicsBackend(GraphicsDevice.BackendType);
                }

                bool vsync = GraphicsDevice.SyncToVerticalBlank;
                if (ImGui.MenuItem("VSync", string.Empty, vsync, true))
                {
                    GraphicsDevice.SyncToVerticalBlank = !vsync;
                }

                bool resizable = Window.Resizable;
                if (ImGui.MenuItem("Resizable Window", string.Empty, resizable))
                {
                    Window.Resizable = !resizable;
                }

                bool bordered = Window.BorderVisible;
                if (ImGui.MenuItem("Visible Window Border", string.Empty, bordered))
                {
                    Window.BorderVisible = !bordered;
                }

                ImGui.EndMenu();
            }
        }

        private void DrawRenderMenu()
        {
            if (ImGui.BeginMenu("Render"))
            {
                if (ImGui.BeginMenu("Camera"))
                {
                    if (ImGui.MenuItem("Primary", string.Empty, _sc.CurrentCamera == _scene.PrimaryCamera))
                    {
                        _sc.CurrentCamera = _scene.PrimaryCamera;
                    }

                    if (ImGui.MenuItem("Secondary", string.Empty, _sc.CurrentCamera == _scene.SecondaryCamera))
                    {
                        _sc.CurrentCamera = _scene.SecondaryCamera;
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Chunk borders"))
                {
                    bool drawChunks = ChunkBorderRenderer.DrawChunks;
                    if (ImGui.MenuItem("Chunks", string.Empty, drawChunks))
                    {
                        ChunkBorderRenderer.DrawChunks = !drawChunks;
                    }

                    bool drawChunkRegions = ChunkBorderRenderer.DrawChunkRegions;
                    if (ImGui.MenuItem("Chunk Regions", string.Empty, drawChunkRegions))
                    {
                        ChunkBorderRenderer.DrawChunkRegions = !drawChunkRegions;
                    }

                    bool drawRenderRegions = ChunkBorderRenderer.DrawRenderRegions;
                    if (ImGui.MenuItem("Render Regions", string.Empty, drawRenderRegions))
                    {
                        ChunkBorderRenderer.DrawRenderRegions = !drawRenderRegions;
                    }

                    bool useDepth = ChunkBorderRenderer.UseDepth;
                    if (ImGui.MenuItem("Use depth", string.Empty, useDepth))
                    {
                        ChunkBorderRenderer.UseDepth = !useDepth;
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMenu();
            }
        }

        private void DrawDebugMenu()
        {
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Refresh Device Objects"))
                {
                    RefreshDeviceObjects(1);
                }

                if (ImGui.MenuItem("Refresh Device Objects (5 times)"))
                {
                    RefreshDeviceObjects(5);
                }

                if (_controllerTracker != null)
                {
                    if (ImGui.MenuItem("Controller State"))
                    {
                        _controllerDebugMenu = true;
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Connect to Controller"))
                    {
                        Sdl2ControllerTracker.CreateDefault(out _controllerTracker);
                        _scene.PrimaryCamera.Controller = _controllerTracker;
                        _scene.SecondaryCamera.Controller = _controllerTracker;
                    }
                }

                ImGui.EndMenu();
            }
        }

        private void DrawRenderDocMenu()
        {
            if (ImGui.BeginMenu("RenderDoc"))
            {
                if (_renderDoc == null)
                {
                    if (ImGui.MenuItem("Load"))
                    {
                        if (RenderDoc.Load(out _renderDoc))
                        {
                            ChangeGraphicsBackend(forceRecreateWindow: true, preferredBackend: GraphicsDevice.BackendType);
                        }
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Trigger Capture"))
                    {
                        _renderDoc.TriggerCapture();
                    }

                    if (ImGui.BeginMenu("Options"))
                    {
                        bool allowVsync = _renderDoc.AllowVSync;
                        if (ImGui.Checkbox("Allow VSync", ref allowVsync))
                        {
                            _renderDoc.AllowVSync = allowVsync;
                        }

                        bool validation = _renderDoc.APIValidation;
                        if (ImGui.Checkbox("API Validation", ref validation))
                        {
                            _renderDoc.APIValidation = validation;
                        }

                        int delayForDebugger = (int)_renderDoc.DelayForDebugger;
                        if (ImGui.InputInt("Debugger Delay", ref delayForDebugger))
                        {
                            delayForDebugger = Math.Clamp(delayForDebugger, 0, int.MaxValue);
                            _renderDoc.DelayForDebugger = (uint)delayForDebugger;
                        }

                        bool verifyBufferAccess = _renderDoc.VerifyBufferAccess;
                        if (ImGui.Checkbox("Verify Buffer Access", ref verifyBufferAccess))
                        {
                            _renderDoc.VerifyBufferAccess = verifyBufferAccess;
                        }

                        bool overlayEnabled = _renderDoc.OverlayEnabled;
                        if (ImGui.Checkbox("Overlay Visible", ref overlayEnabled))
                        {
                            _renderDoc.OverlayEnabled = overlayEnabled;
                        }

                        bool overlayFrameRate = _renderDoc.OverlayFrameRate;
                        if (ImGui.Checkbox("Overlay Frame Rate", ref overlayFrameRate))
                        {
                            _renderDoc.OverlayFrameRate = overlayFrameRate;
                        }

                        bool overlayFrameNumber = _renderDoc.OverlayFrameNumber;
                        if (ImGui.Checkbox("Overlay Frame Number", ref overlayFrameNumber))
                        {
                            _renderDoc.OverlayFrameNumber = overlayFrameNumber;
                        }

                        bool overlayCaptureList = _renderDoc.OverlayCaptureList;
                        if (ImGui.Checkbox("Overlay Capture List", ref overlayCaptureList))
                        {
                            _renderDoc.OverlayCaptureList = overlayCaptureList;
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Launch Replay UI"))
                    {
                        _renderDoc.LaunchReplayUI();
                    }
                }
                ImGui.EndMenu();
            }
        }

        private void DrawControllerDebugMenu()
        {
            if (_controllerDebugMenu)
            {
                if (ImGui.Begin("Controller State", ref _controllerDebugMenu, ImGuiWindowFlags.NoCollapse))
                {
                    if (_controllerTracker != null)
                    {
                        ImGui.Columns(2);

                        ImGui.Text($"Name: {_controllerTracker.ControllerName}");
                        foreach (SDL_GameControllerAxis axis in Enum.GetValues<SDL_GameControllerAxis>())
                        {
                            ImGui.Text($"{axis}: {_controllerTracker.GetAxis(axis)}");
                        }

                        ImGui.NextColumn();

                        foreach (SDL_GameControllerButton button in Enum.GetValues<SDL_GameControllerAxis>())
                        {
                            ImGui.Text($"{button}: {_controllerTracker.IsPressed(button)}");
                        }
                    }
                    else
                    {
                        ImGui.Text("No controller detected.");
                    }
                }
                ImGui.End();
            }
        }

        private void DrawProfiler(List<Profiler.FrameSet> frameSets)
        {
            using ProfilerPopToken profilerToken = _sc.Profiler.Push();

            if (frameSets.Count > 0)
            {
                if (ImGui.Begin("Profiler"))
                {
                    if (_renderRegionManager.ChunkMeshHeap is HeapPool heapPool)
                    {
                        ImGui.Text((heapPool.AvailableBytes / 1024) + "kB");
                    }

                    void SameLineFor(string text)
                    {
                        ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize(text).X + 8);
                    }

                    void DrawFrameSet(int setIndex, int parentIndex)
                    {
                        Profiler.FrameSet frameSet = frameSets[setIndex];

                        List<Profiler.Item> items = frameSet.Items;

                        HashSet<string> memberNames = new();
                        HashSet<string> duplicateMemberNames = new();

                        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                        {
                            Profiler.Item item = items[itemIndex];

                            if (parentIndex != item.ParentOffset)
                                continue;

                            if (!memberNames.Add(item.MemberName))
                                duplicateMemberNames.Add(item.MemberName);
                        }

                        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                        {
                            Profiler.Item item = items[itemIndex];

                            if (parentIndex != item.ParentOffset)
                                continue;

                            if (setIndex + 1 < frameSets.Count)
                            {
                                bool hasMatchingSubItems = false;
                                foreach (Profiler.Item subItem in frameSets[setIndex + 1].Items)
                                {
                                    if (subItem.ParentOffset == itemIndex)
                                    {
                                        hasMatchingSubItems = true;
                                        break;
                                    }
                                }

                                if (hasMatchingSubItems)
                                {
                                    if (ImGui.CollapsingHeader(item.MemberName))
                                    {
                                        string durationText = item.Duration.TotalMilliseconds.ToString("0.000");
                                        SameLineFor(durationText);
                                        ImGui.Text(durationText);

                                        ImGui.TreePush();
                                        DrawFrameSet(setIndex + 1, itemIndex);
                                        ImGui.TreePop();
                                    }
                                    else
                                    {
                                        string durationText = item.Duration.TotalMilliseconds.ToString("0.000");
                                        SameLineFor(durationText);
                                        ImGui.Text(durationText);
                                    }
                                    continue;
                                }
                            }

                            {
                                if (duplicateMemberNames.Contains(item.MemberName))
                                {
                                    string fileName = Path.GetFileNameWithoutExtension(item.FilePath);
                                    ImGui.Text($"{fileName}.{item.MemberName}");
                                }
                                else
                                {
                                    ImGui.Text(item.MemberName);
                                }

                                string durationText = item.Duration.TotalMilliseconds.ToString("0.000");
                                SameLineFor(durationText);
                                ImGui.Text(durationText);
                            }
                        }
                    }

                    DrawFrameSet(0, 0);

                    ImGui.End();
                }
            }
        }

        private void RefreshDeviceObjects(int numTimes = 1)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < numTimes; i++)
            {
                GraphicsDevice.WaitForIdle();
                DisposeGraphicsDeviceObjects();
                CreateGraphicsDeviceObjects();
            }
            sw.Stop();
            Console.WriteLine($"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds.");
        }

        private static Skybox GetDefaultSkybox()
        {
            Skybox skybox = new((sc) =>
            {
                Image<Rgba32> front = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_ft.png"));
                Image<Rgba32> back = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_bk.png"));
                Image<Rgba32> left = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_lf.png"));
                Image<Rgba32> right = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_rt.png"));
                Image<Rgba32> top = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_up.png"));
                Image<Rgba32> bottom = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_dn.png"));

                ImageSharpCubemapTexture cubemap = new(right, left, top, bottom, back, front, false);
                return cubemap;
            });

            skybox.TextureLoaded += (sc, cubemap) =>
            {
                foreach (Image<Rgba32>[] imageArray in cubemap.CubemapTextures)
                {
                    foreach (Image<Rgba32> image in imageArray)
                        image.Dispose();
                }
            };

            return skybox;
        }

        private void AddObjModel(string dir, string obj, string mtl)
        {
            ObjFile file;
            using (FileStream objStream = File.OpenRead(AssetHelper.GetPath(Path.Combine(dir, obj))))
                file = new ObjParser().Parse(objStream);

            MtlFile mtls;
            using (FileStream mtlStream = File.OpenRead(AssetHelper.GetPath(Path.Combine(dir, mtl))))
                mtls = new MtlParser().Parse(mtlStream);

            foreach (ObjFile.MeshGroup group in file.MeshGroups)
            {
                Vector3 scale = new(0.1f);
                ConstructedMesh mesh = file.GetMesh32(group, reduce: true); // TODO: dynamic mesh get
                MaterialDefinition materialDef = mtls.Definitions[mesh.MaterialName];
                ImageSharpTexture? overrideTextureData = null;
                ImageSharpTexture? alphaTexture = null;

                if (materialDef.DiffuseTexture != null)
                {
                    string texturePath = AssetHelper.GetPath(Path.Combine(dir, materialDef.DiffuseTexture));
                    overrideTextureData = LoadTexture(texturePath, true);
                }
                if (materialDef.AlphaMap != null)
                {
                    string texturePath = AssetHelper.GetPath(Path.Combine(dir, materialDef.AlphaMap));
                    alphaTexture = LoadTexture(texturePath, false);
                }

                TexturedMesh texturedMesh = AddTexturedMesh(
                    mesh,
                    overrideTextureData,
                    alphaTexture,
                    Vector3.Zero,
                    Quaternion.Identity,
                    scale,
                    group.Name);
            }
        }

        // Plz don't call this with the same texturePath and different mipmap values.
        private ImageSharpTexture LoadTexture(string texturePath, bool mipmap)
        {
            lock (_textures)
            {
                if (!_textures.TryGetValue(texturePath, out ImageSharpTexture? tex))
                {
                    tex = new ImageSharpTexture(texturePath, mipmap, true);
                    _textures.Add(texturePath, tex);
                }
                return tex;
            }
        }

        private TexturedMesh AddTexturedMesh(
            ConstructedMesh meshData,
            ImageSharpTexture? texData,
            ImageSharpTexture? alphaTexData,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            string name)
        {
            TexturedMesh mesh = new(name, meshData, texData, alphaTexData);
            mesh.Transform.Set(position, rotation, scale);
            AddRenderable(mesh);

            return mesh;
        }

        private void AddRenderable(Renderable renderable)
        {
            _queuedRenderables.Enqueue(renderable);
        }

        private void ToggleFullscreenState()
        {
            bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
            Window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
        }

        protected override void Dispose(bool disposing)
        {
            ChunkRenderer?.Dispose();
            ChunkBorderRenderer.Dispose();

            base.Dispose(disposing);
        }
    }
}
