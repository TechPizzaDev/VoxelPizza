using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using VoxelPizza.Client.Input;

namespace VoxelPizza.Client
{
    public unsafe abstract class Application : IDisposable
    {
        public static readonly bool GraphicsDebugDefault =
#if DEBUG
            true;
#else
            false;
#endif

        private Sdl2Window _window;
        private GraphicsDevice _graphicsDevice;

        private long _totalTicks;
        private long _previousTicks;

        private Action _enableScreensaver;
        private Action _disableScreensaver;
        private SDL_EventFilter _sdlEventWatch;

        private bool _drawWhenUnfocused = true;
        private TimeSpan _inactiveFrameTime = TimeSpan.FromSeconds(1 / 20.0);

        private bool _shouldExit;
        private bool _inRun;

        public TimeAverager TimeAverager { get; private set; }

        public bool IsDisposed { get; private set; }
        public bool IsActive { get; private set; }

        public bool AlwaysRecreateWindow { get; set; } = true;
        public bool SrgbSwapchain { get; set; } = false;

        public string WindowTitle = "VoxelPizza";

        public GraphicsDevice GraphicsDevice => _graphicsDevice;
        public Sdl2Window Window
        {
            get => _window;
            private set
            {
                _window = value ?? throw new ArgumentNullException(nameof(value));
                _window.Resized += () => WindowResized();
                _window.Exposed += () => WindowExposed();
                _window.FocusGained += () => WindowGainedFocus();
                _window.FocusLost += () => WindowLostFocus();
            }
        }

        public Application(GraphicsBackend? preferredBackend = null)
        {
            WindowCreateInfo windowCI = new()
            {
                X = 50,
                Y = 50,
                WindowWidth = 960,
                WindowHeight = 540,
                WindowInitialState = WindowState.Hidden,
                WindowTitle = WindowTitle
            };

            GraphicsDeviceOptions gdOptions = new(
                ShouldEnableGraphicsDeviceDebug(), null, false, ResourceBindingModel.Improved, true, true, SrgbSwapchain);

            try
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(
                    windowCI,
                    gdOptions,
                    preferredBackend ?? VeldridStartup.GetPlatformDefaultBackend(),
                    out _window,
                    out _graphicsDevice);
            }
            catch
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(
                    windowCI,
                    gdOptions,
                    VeldridStartup.GetPlatformDefaultBackend(),
                    out _window,
                    out _graphicsDevice);
            }
            Window = _window;

            _enableScreensaver = Sdl2Native.LoadFunction<Action>("SDL_EnableScreenSaver");
            _disableScreensaver = Sdl2Native.LoadFunction<Action>("SDL_DisableScreenSaver");

            _sdlEventWatch = SdlEventWatch;
            Sdl2Native.SDL_AddEventWatch(_sdlEventWatch, null);

            TimeAverager = new TimeAverager(4, TimeSpan.FromSeconds(0.5));

            WindowGainedFocus();
        }

        protected virtual void WindowGainedFocus()
        {
            IsActive = true;
            _disableScreensaver.Invoke();
        }

        protected virtual void WindowLostFocus()
        {
            IsActive = false;
            _enableScreensaver.Invoke();
        }

        private unsafe int SdlEventWatch(void* data, SDL_Event* @event)
        {
            if (@event->type == SDL_EventType.WindowEvent)
            {
                SDL_WindowEvent windowEvent = Unsafe.Read<SDL_WindowEvent>(@event);
                if (windowEvent.@event == SDL_WindowEventID.Exposed)
                {
                    PumpSdlEvents();
                    WindowExposed();
                }
            }
            return 0;
        }

        public void Run()
        {
            _totalTicks = 0;
            _previousTicks = Stopwatch.GetTimestamp();

            PumpSdlEvents();
            if (RunOnce(true))
            {
                Window.Visible = true;

                while (Window.Exists)
                {
                    PumpSdlEvents();
                    if (!RunOnce(false))
                        break;
                }
            }

            DisposeGraphicsDevice();
            _graphicsDevice = null!;
        }

        public bool RunOnce(bool forceDraw)
        {
            try
            {
                return RunBody(forceDraw);
            }
            finally
            {
                TimeAverager.Tick();
            }
        }

        protected virtual bool RunBody(bool forceDraw)
        {
            if (_inRun)
            {
                throw new InvalidOperationException();
            }
            _inRun = true;

            try
            {
                long currentTicks = Stopwatch.GetTimestamp();
                long deltaTicks = currentTicks - _previousTicks;
                _previousTicks = currentTicks;
                _totalTicks += deltaTicks;

                FrameTime time = new(
                    TimeSpan.FromSeconds(_totalTicks * TimeAverager.SecondsPerTick),
                    TimeSpan.FromSeconds(deltaTicks * TimeAverager.SecondsPerTick),
                    IsActive);

                TimeAverager.BeginUpdate();
                Update(time);
                TimeAverager.EndUpdate();

                if (_shouldExit)
                {
                    Window.Close();
                    _shouldExit = false;
                    return false;
                }

                if (!forceDraw && !Window.Exists || _graphicsDevice == null)
                {
                    return false;
                }

                WindowState windowState = Window.WindowState;
                bool hasSurface = forceDraw || windowState is not WindowState.Minimized and not WindowState.Hidden;

                if (time.IsActive)
                {
                    if (hasSurface)
                    {
                        DrawAndPresent();
                    }
                }
                else
                {
                    if (hasSurface && _drawWhenUnfocused)
                    {
                        DrawAndPresent();
                    }

                    double spentMillis = (Stopwatch.GetTimestamp() - currentTicks) * TimeAverager.MillisPerTick;
                    int millis = (int)(_inactiveFrameTime.TotalMilliseconds - spentMillis);
                    if (millis > 0)
                    {
                        Thread.Sleep(millis);
                    }
                }
                return true;
            }
            finally
            {
                _inRun = false;
            }
        }

        public void Exit()
        {
            _shouldExit = true;
        }

        private void PumpSdlEvents()
        {
            InputSnapshot snapshot = Window.PumpEvents();
            InputTracker.UpdateFrameInput(snapshot, Window);
        }

        public virtual void Update(in FrameTime time)
        {
        }

        public virtual void Draw()
        {
        }

        public virtual void Present()
        {
            _graphicsDevice.SwapBuffers();
        }

        private void DrawAndPresent()
        {
            // TODO: try to revive other backends
            try
            {
                TimeAverager.BeginDraw();
                Draw();
                TimeAverager.EndDraw();
            }
            catch (SharpGen.Runtime.SharpGenException ex) when
                (ex.ResultCode == Vortice.DXGI.ResultCode.DeviceRemoved ||
                ex.ResultCode == Vortice.DXGI.ResultCode.DeviceReset)
            {
                Console.WriteLine(ex); // TODO: log proper error

                ChangeGraphicsBackend(true, null);
                return;
            }

            TimeAverager.BeginPresent();
            Present();
            TimeAverager.EndPresent();
        }

        public void ChangeGraphicsBackend(bool forceRecreateWindow, GraphicsBackend? preferredBackend = null)
        {
            GraphicsBackend previousBackend = _graphicsDevice.BackendType;
            bool syncToVBlank = _graphicsDevice.SyncToVerticalBlank;

            DisposeGraphicsDevice();
            _graphicsDevice = null!;

            if (AlwaysRecreateWindow || forceRecreateWindow)
            {
                WindowCreateInfo windowCI = new()
                {
                    X = Window.X,
                    Y = Window.Y,
                    WindowWidth = Window.Width,
                    WindowHeight = Window.Height,
                    WindowInitialState = Window.WindowState,
                    WindowTitle = WindowTitle
                };

                Window.Close();
                Window = VeldridStartup.CreateWindow(windowCI);
            }

            GraphicsDeviceOptions gdOptions = new(
                ShouldEnableGraphicsDeviceDebug(), null, syncToVBlank, ResourceBindingModel.Improved, true, true, SrgbSwapchain);

            try
            {
                if (preferredBackend == null)
                    preferredBackend = previousBackend;

                _graphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions, preferredBackend.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex); // TODO: log proper error

                _graphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions);
            }

            CreateGraphicsDeviceObjects();
        }

        private void DisposeGraphicsDevice()
        {
            GraphicsDevice.WaitForIdle();
            DisposeGraphicsDeviceObjects();
            GraphicsDevice.WaitForIdle();
            GraphicsDevice.Dispose();
        }

        protected virtual bool ShouldEnableGraphicsDeviceDebug()
        {
            return GraphicsDebugDefault;
        }

        protected virtual void WindowResized()
        {
        }

        protected virtual void WindowExposed()
        {
            if (!_inRun)
            {
                RunOnce(false);
            }
        }

        protected void ChangeGraphicsBackend(GraphicsBackend? preferredBackend = null)
        {
            ChangeGraphicsBackend(false, preferredBackend);
        }

        protected virtual void CreateGraphicsDeviceObjects()
        {
        }

        protected virtual void DisposeGraphicsDeviceObjects()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
            }
        }

        ~Application()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
