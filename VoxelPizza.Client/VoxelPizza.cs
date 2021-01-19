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
using Veldrid.Sdl2;
using Veldrid.Utilities;
using VoxelPizza.Client.Objects;

namespace VoxelPizza.Client
{
    public class VoxelPizza : Application
    {
        public static RenderDoc? _renderDoc;

        private Sdl2ControllerTracker? _controllerTracker;

        private Scene _scene;
        private SceneContext _sc = new SceneContext();

        private CommandList _frameCommands;
        private CommandList _updateCommands;

        private ImGuiRenderable _imGuiRenderable;
        private FullScreenQuad _fsq;
        private bool _controllerDebugMenu;

        private readonly string[] _msaaOptions = new string[] { "Off", "2x", "4x", "8x", "16x", "32x" };
        private int _msaaOption = 0;
        private TextureSampleCount? _newSampleCount;

        private List<Task> _loadTasks = new List<Task>();
        private ConcurrentQueue<Renderable> _queuedRenderables = new ConcurrentQueue<Renderable>();
        private Dictionary<string, ImageSharpTexture> _textures = new Dictionary<string, ImageSharpTexture>();
        private ConcurrentBag<TexturedMesh> _ditheredMeshes = new ConcurrentBag<TexturedMesh>();

        private event Action<int, int> _resizeHandled;
        private bool _windowResized;

        private ParticlePlane particlePlane;

        public VoxelPizza() : base(preferredBackend: GraphicsBackend.Direct3D11)
        {
            Sdl2Native.SDL_Init(SDLInitFlags.GameController);
            Sdl2ControllerTracker.CreateDefault(out _controllerTracker);

            GraphicsDevice.SyncToVerticalBlank = true;

            _scene = new Scene(GraphicsDevice, Window);
            _scene.Camera.Controller = _controllerTracker;
            _sc.SetCurrentScene(_scene);

            _imGuiRenderable = new ImGuiRenderable(Window.Width, Window.Height);
            _resizeHandled += (w, h) => _imGuiRenderable.WindowResized(w, h);
            _scene.AddRenderable(_imGuiRenderable);
            _scene.AddUpdateable(_imGuiRenderable);

            _sc.Camera.Position = new Vector3(-120, 25, -4.3f);
            _sc.Camera.Yaw = -MathF.PI / 2;
            _sc.Camera.Pitch = 0;

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


            _loadTasks.Add(Task.Run(() =>
            {
                Skybox skybox = LoadDefaultSkybox();
                AddRenderable(skybox);
            }));

            _loadTasks.Add(Task.Run(() =>
            {
                //string dir = "Models/SponzaAtrium";
                //string obj = "sponza.obj";
                //string mtl = "sponza.mtl";

                //AddObjModel(dir, obj, mtl);
            }));

            CreateGraphicsDeviceObjects();
            ImGui.StyleColorsClassic();
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
            GraphicsDevice.WaitForIdle();

            _updateCommands.Dispose();
            _frameCommands.Dispose();
            StaticResourceCache.DisposeGraphicsDeviceObjects();

            _sc.DisposeGraphicsDeviceObjects();
            _scene.DisposeGraphicsDeviceObjects();
            CommonMaterials.DisposeGraphicsDeviceObjects();

            GraphicsDevice.WaitForIdle();
        }

        protected override void CreateGraphicsDeviceObjects()
        {
            _updateCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
            _updateCommands.Name = "Update Commands List";

            _frameCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
            _frameCommands.Name = "Frame Commands List";

            using CommandList initCL = GraphicsDevice.ResourceFactory.CreateCommandList();
            initCL.Name = "Recreation Initialization Command List";
            initCL.Begin();
            {
                _sc.CreateGraphicsDeviceObjects(GraphicsDevice, initCL, _sc);
                CommonMaterials.CreateGraphicsDeviceObjects(GraphicsDevice, initCL, _sc);
                _scene.CreateGraphicsDeviceObjects(GraphicsDevice, initCL, _sc);
            }
            initCL.End();
            GraphicsDevice.SubmitCommands(initCL);

            _scene.Camera.UpdateGraphicsBackend(GraphicsDevice, Window);
        }

        public override void Update(in FrameTime time)
        {
            _updateCommands.Begin();
            {
                while (_queuedRenderables.TryDequeue(out Renderable? renderable))
                {
                    _scene.AddRenderable(renderable);
                    renderable.CreateDeviceObjects(GraphicsDevice, _updateCommands, _sc);
                }
            }
            _updateCommands.End();
            GraphicsDevice.SubmitCommands(_updateCommands);
            
            _scene.Update(time);

            DrawMainMenu();

            particlePlane?.Update(time);

            if (InputTracker.GetKeyDown(Key.F11))
            {
                ToggleFullscreenState();
            }
        }

        public override void Draw()
        {
            int width = Window.Width;
            int height = Window.Height;

            if (_windowResized)
            {
                _windowResized = false;

                GraphicsDevice.ResizeMainWindow((uint)width, (uint)height);
                _scene.Camera.WindowResized(width, height);
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

            _frameCommands.Begin();
            {
                CommonMaterials.UpdateAll(_frameCommands);
                _scene.RenderAllStages(GraphicsDevice, _frameCommands, _sc);
            }
            _frameCommands.End();
            GraphicsDevice.SubmitCommands(_frameCommands);
        }

        private void DrawMainMenu()
        {
            if (ImGui.BeginMainMenuBar())
            {
                DrawSettingsMenu();
                DrawWindowMenu();
                DrawMaterialsMenu();
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
                ImGui.Text("Prepare");
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
                    GraphicsDevice.SyncToVerticalBlank = !GraphicsDevice.SyncToVerticalBlank;
                }

                bool resizable = Window.Resizable;
                if (ImGui.MenuItem("Resizable Window", string.Empty, resizable))
                {
                    Window.Resizable = !Window.Resizable;
                }

                bool bordered = Window.BorderVisible;
                if (ImGui.MenuItem("Visible Window Border", string.Empty, bordered))
                {
                    Window.BorderVisible = !Window.BorderVisible;
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
                        _scene.Camera.Controller = _controllerTracker;
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
                            ChangeGraphicsBackend(GraphicsDevice.BackendType, forceRecreateWindow: true);
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

        private static Skybox LoadDefaultSkybox()
        {
            return new Skybox(
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_ft.png")),
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_bk.png")),
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_lf.png")),
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_rt.png")),
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_up.png")),
                Image.Load<Rgba32>(AssetHelper.GetPath("Textures/cloudtop/cloudtop_dn.png")));
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

                _ditheredMeshes.Add(texturedMesh);
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
