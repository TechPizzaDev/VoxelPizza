using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;
using Veldrid.Utilities;
using VoxelPizza.Client.Objects;
using VoxelPizza.Diagnostics;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public class VoxelPizza : Application
    {
        private static RenderDoc? _renderDoc;

        private Sdl2ControllerTracker? _controllerTracker;

        private Scene _scene;
        private SceneContext _sc;

        private CommandList _frameCommands;

        private ImGuiRenderable _imGuiRenderable;
        private FullScreenQuad _fsq;
        private bool _controllerDebugMenu;

        private readonly string[] _msaaOptions = new string[] { "Off", "2x", "4x", "8x", "16x", "32x" };
        private int _msaaOption = 0;
        private TextureSampleCount? _newSampleCount;

        private List<Task> _loadTasks = new();
        private ConcurrentQueue<Renderable> _queuedRenderables = new();
        private Dictionary<string, ImageSharpTexture> _textures = new();

        private event Action<int, int> _resizeHandled;
        private bool _windowResized;

        private ParticlePlane particlePlane;

        private WorldManager _worldManager;
        private Dimension _currentDimension;

        public ChunkRenderer ChunkRenderer { get; }
        public ChunkBorderRenderer ChunkBorderRenderer { get; }

        public VoxelPizza() : base(preferredBackend: GraphicsBackend.Vulkan)
        {
            Sdl2Native.SDL_Init(SDLInitFlags.GameController);
            Sdl2ControllerTracker.CreateDefault(out _controllerTracker);

            GraphicsDevice.SyncToVerticalBlank = true;

            _sc = new SceneContext();
            _sc.Profiler = new Profiler();
            _sc.CameraChanged += Scene_CameraChanged;

            _scene = new Scene(GraphicsDevice, Window);

            _scene.PrimaryCamera.Controller = _controllerTracker;
            _sc.AddCamera(_scene.PrimaryCamera);

            _scene.SecondaryCamera.Controller = _controllerTracker;
            _sc.AddCamera(_scene.SecondaryCamera);

            _scene.PrimaryCamera.Position = new Vector3(-6, 24f, -0.43f);
            _scene.PrimaryCamera.Yaw = MathF.PI * 1.25f;
            _scene.PrimaryCamera.Pitch = 0;

            _imGuiRenderable = new ImGuiRenderable(Window.Width, Window.Height);
            _resizeHandled += (w, h) => _imGuiRenderable.WindowResized(w, h);
            _scene.AddRenderable(_imGuiRenderable);

            ShadowmapDrawer texDrawIndexeder = new ShadowmapDrawer(() => Window, () => _sc.NearShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder.OnWindowResized();
            texDrawIndexeder.Position = new Vector2(10, 25);
            //_scene.AddRenderable(texDrawIndexeder);

            ShadowmapDrawer texDrawIndexeder2 = new ShadowmapDrawer(() => Window, () => _sc.MidShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder2.OnWindowResized();
            texDrawIndexeder2.Position = new Vector2(20 + texDrawIndexeder2.Size.X, 25);
            //_scene.AddRenderable(texDrawIndexeder2);

            ShadowmapDrawer texDrawIndexeder3 = new ShadowmapDrawer(() => Window, () => _sc.FarShadowMapView);
            _resizeHandled += (w, h) => texDrawIndexeder3.OnWindowResized();
            texDrawIndexeder3.Position = new Vector2(30 + (texDrawIndexeder3.Size.X * 2), 25);
            //_scene.AddRenderable(texDrawIndexeder3);

            ScreenDuplicator duplicator = new ScreenDuplicator();
            _scene.AddRenderable(duplicator);

            _fsq = new FullScreenQuad();
            _scene.AddRenderable(_fsq);

            //particlePlane = new ParticlePlane(_scene.Camera);
            //_scene.AddRenderable(particlePlane);

            _worldManager = new WorldManager();
            _currentDimension = _worldManager.CreateDimension();

            var chunkMeshPool = new HeapPool(1024 * 1024 * 16);
            ChunkRenderer = new ChunkRenderer(_currentDimension, chunkMeshPool, new Size3(4, 3, 4));
            ChunkRenderer.CullCamera = _scene.PrimaryCamera;
            _scene.AddUpdateable(ChunkRenderer);
            _scene.AddRenderable(ChunkRenderer);

            ChunkBorderRenderer = new ChunkBorderRenderer(ChunkRenderer);
            _scene.AddUpdateable(ChunkBorderRenderer);
            _scene.AddRenderable(ChunkBorderRenderer);

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
        }

        private void Scene_CameraChanged(Camera? camera)
        {
            ChunkRenderer.RenderCamera = camera;
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
            _frameCommands.Dispose();
            StaticResourceCache.DisposeGraphicsDeviceObjects();

            _sc.DisposeGraphicsDeviceObjects();
            _scene.DestroyGraphicsDeviceObjects();
            CommonMaterials.DisposeGraphicsDeviceObjects();
        }

        protected override void CreateGraphicsDeviceObjects()
        {
            _frameCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
            _frameCommands.Name = "Frame Commands List";

            using CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
            cl.Name = "Recreation Initialization Command List";
            cl.Begin();
            {
                CommonMaterials.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
                _sc.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
                _scene.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
            }
            cl.End();
            GraphicsDevice.SubmitCommands(cl);

            _scene.PrimaryCamera.UpdateGraphicsBackend(GraphicsDevice, Window);
        }

        private List<Profiler.FrameSet> frameSets = new();

        protected override bool RunBody()
        {
            Profiler? profiler = _sc.Profiler;
            profiler?.Start();
            try
            {
                return base.RunBody();
            }
            finally
            {
                if (profiler != null)
                {
                    profiler.Stop();

                    var sets = profiler._sets;
                    if (sets.Count > 0)
                    {
                        for (int i = 0; i < sets.Count; i++)
                        {
                            if (frameSets.Count <= i)
                                frameSets.Add(new Profiler.FrameSet());

                            frameSets[i].Items.Clear();
                            frameSets[i].Items.AddRange(sets[i].Items);
                            frameSets[i].Offset = sets[i].Offset;
                        }
                    }

                    profiler.Clear();
                }
            }
        }

        public override void Update(in FrameTime time)
        {
            using var profilerToken = _sc.Profiler.Push();

            _imGuiRenderable.Update(time);

            UpdateScene(time);

            particlePlane?.Update(time);

            //_sc.DirectionalLight.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(time.TotalSeconds));

            if (InputTracker.GetKeyDown(Key.F11))
            {
                ToggleFullscreenState();
            }
        }

        private void UpdateScene(in FrameTime time)
        {
            using var profilerToken = _sc.Profiler.Push();

            _scene.Update(time, _sc);
        }

        public override void Draw()
        {
            using var profilerToken = _sc.Profiler.Push();

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

            _frameCommands.Begin();
            {
                while (_queuedRenderables.TryDequeue(out Renderable? renderable))
                {
                    _scene.AddRenderable(renderable);
                    renderable.CreateDeviceObjects(GraphicsDevice, _frameCommands, _sc);
                }

                CommonMaterials.UpdateAll(_frameCommands);
                _scene.RenderAllStages(GraphicsDevice, _frameCommands, _sc);
            }
            _frameCommands.End();
            GraphicsDevice.SubmitCommands(_frameCommands);
        }

        public override void Present()
        {
            using var profilerToken = _sc.Profiler.Push();

            base.Present();
        }

        private void DrawOverlay()
        {
            using var profilerToken = _sc.Profiler.Push();

            DrawMainMenu();

            DrawProfiler(frameSets);
        }

        private void DrawMainMenu()
        {
            using var profilerToken = _sc.Profiler.Push();

            if (ImGui.BeginMainMenuBar())
            {
                DrawSettingsMenu();
                DrawWindowMenu();
                DrawRenderMenu();
                DrawMaterialsMenu();
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
            var gd = GraphicsDevice;

            if (ImGui.BeginMenu("Settings"))
            {
                if (ImGui.BeginMenu("Graphics Backend"))
                {
                    if (ImGui.MenuItem("Vulkan", string.Empty, gd.BackendType == GraphicsBackend.Vulkan, GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Vulkan);
                    }
                    if (ImGui.MenuItem("OpenGL", string.Empty, gd.BackendType == GraphicsBackend.OpenGL, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGL)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.OpenGL);
                    }
                    if (ImGui.MenuItem("OpenGL ES", string.Empty, gd.BackendType == GraphicsBackend.OpenGLES, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGLES)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.OpenGLES);
                    }
                    if (ImGui.MenuItem("Direct3D 11", string.Empty, gd.BackendType == GraphicsBackend.Direct3D11, GraphicsDevice.IsBackendSupported(GraphicsBackend.Direct3D11)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Direct3D11);
                    }
                    if (ImGui.MenuItem("Metal", string.Empty, gd.BackendType == GraphicsBackend.Metal, GraphicsDevice.IsBackendSupported(GraphicsBackend.Metal)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Metal);
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

        private void DrawMaterialsMenu()
        {
            if (ImGui.BeginMenu("Materials"))
            {
                if (ImGui.BeginMenu("Brick"))
                {
                    DrawIndexedMaterialMenu(CommonMaterials.Brick);
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Vase"))
                {
                    DrawIndexedMaterialMenu(CommonMaterials.Vase);
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
                        foreach (SDL_GameControllerAxis axis in (SDL_GameControllerAxis[])Enum.GetValues(typeof(SDL_GameControllerAxis)))
                        {
                            ImGui.Text($"{axis}: {_controllerTracker.GetAxis(axis)}");
                        }

                        ImGui.NextColumn();

                        foreach (SDL_GameControllerButton button in (SDL_GameControllerButton[])Enum.GetValues(typeof(SDL_GameControllerButton)))
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
            using var profilerToken = _sc.Profiler.Push();

            if (frameSets.Count > 0)
            {
                if (ImGui.Begin("Profiler"))
                {
                    void SameLineFor(string text)
                    {
                        ImGui.SameLine(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize(text).X + 8);
                    }

                    void DrawFrameSet(int setIndex, int parentIndex)
                    {
                        Profiler.FrameSet frameSet = frameSets[setIndex];

                        List<Profiler.Item> items = frameSet.Items;

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
                                ImGui.Text(item.MemberName);

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
                DisposeGraphicsDeviceObjects();
                CreateGraphicsDeviceObjects();
            }
            sw.Stop();
            Console.WriteLine($"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds.");
        }

        private static Skybox GetDefaultSkybox()
        {
            var skybox = new Skybox((sc) =>
            {
                Image<Rgba32> front = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_ft.png"));
                Image<Rgba32> back = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_bk.png"));
                Image<Rgba32> left = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_lf.png"));
                Image<Rgba32> right = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_rt.png"));
                Image<Rgba32> top = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_up.png"));
                Image<Rgba32> bottom = Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_dn.png"));

                var cubemap = new ImageSharpCubemapTexture(right, left, top, bottom, back, front, false);
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
                Vector3 scale = new Vector3(0.1f);
                ConstructedMesh mesh = file.GetMesh32(group, reduce: true); // TODO: dynamic mesh get
                MaterialDefinition materialDef = mtls.Definitions[mesh.MaterialName];
                ImageSharpTexture? overrideTextureData = null;
                ImageSharpTexture? alphaTexture = null;
                MaterialPropertyBuffer materialProps = CommonMaterials.Brick;

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

                if (materialDef.Name.Contains("vase"))
                {
                    materialProps = CommonMaterials.Vase;
                }
                if (group.Name == "sponza_117")
                {
                    materialProps = CommonMaterials.Brick;
                }

                var texturedMesh = AddTexturedMesh(
                    mesh,
                    overrideTextureData,
                    alphaTexture,
                    materialProps,
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
            MaterialPropertyBuffer materialProps,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            string name)
        {
            TexturedMesh mesh = new(name, meshData, texData, alphaTexData, materialProps ?? CommonMaterials.Brick);
            mesh.Transform.Set(position, rotation, scale);
            AddRenderable(mesh);

            return mesh;
        }

        private void AddRenderable(Renderable renderable)
        {
            _queuedRenderables.Enqueue(renderable);
        }

        private static void DrawIndexedMaterialMenu(MaterialPropertyBuffer propsAndBuffer)
        {
            MaterialProperties props = propsAndBuffer.Properties;
            float intensity = props.SpecularIntensity.X;
            if (ImGui.SliderFloat("Intensity", ref intensity, 0f, 10f, intensity.ToString(), 1f)
                | ImGui.SliderFloat("Power", ref props.SpecularPower, 0f, 1000f, props.SpecularPower.ToString(), 1f))
            {
                props.SpecularIntensity = new Vector3(intensity);
                propsAndBuffer.Properties = props;
            }
        }

        private void ToggleFullscreenState()
        {
            bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
            Window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
        }
    }
}
