using System;
using System.Diagnostics;
using System.Threading;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace VoxelPizza.Client
{
    public abstract class Application
    {
        private Sdl2Window _window;
        private GraphicsDevice _graphicsDevice;

        private Action _enableScreensaver;
        private Action _disableScreensaver;

        private bool _drawWhenUnfocused = true;
        private bool _drawWhenMinimized = false;
        private TimeSpan _inactiveFrameTime = TimeSpan.FromSeconds(1 / 20.0);

        private bool _shouldExit;

        public TimeAverager TimeAverager { get; private set; }

        public bool IsActive { get; private set; }

        public bool AlwaysRecreateWindow { get; set; } = true;
        public bool SrgbSwapchain { get; set; } = false;

        public string WindowTitle = "VoxelPizza";

        public GraphicsDevice GraphicsDevice
        {
            get => _graphicsDevice;
            private set
            {
                if(value == null)
                    throw new ArgumentNullException(nameof(value));

                _graphicsDevice = value;
            }
        }

        public Sdl2Window Window
        {
            get => _window;
            private set
            {
                _window = value ?? throw new ArgumentNullException(nameof(value));
                _window.Resized += () => WindowResized();
                _window.FocusGained += Window_FocusGained;
                _window.FocusLost += Window_FocusLost;
            }
        }

        public Application(GraphicsBackend? preferredBackend = null)
        {
            var windowCI = new WindowCreateInfo
            {
                X = 50,
                Y = 50,
                WindowWidth = 960,
                WindowHeight = 540,
                WindowInitialState = WindowState.Hidden,
                WindowTitle = WindowTitle
            };

            var gdOptions = new GraphicsDeviceOptions(
                ShouldEnableGraphicsDeviceDebug(), null, false, ResourceBindingModel.Improved, true, true, SrgbSwapchain);

            GraphicsDevice gd;
            Sdl2Window window;
            try
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(
                    windowCI,
                    gdOptions,
                    preferredBackend ?? VeldridStartup.GetPlatformDefaultBackend(),
                    out window,
                    out gd);
            }
            catch
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(
                    windowCI,
                    gdOptions,
                    VeldridStartup.GetPlatformDefaultBackend(),
                    out window,
                    out gd);
            }
            _enableScreensaver = Sdl2Native.LoadFunction<Action>("SDL_EnableScreenSaver");
            _disableScreensaver = Sdl2Native.LoadFunction<Action>("SDL_DisableScreenSaver");

            TimeAverager = new TimeAverager(4, TimeSpan.FromSeconds(0.5));
            GraphicsDevice = gd;
            Window = window;
        }

        private void Window_FocusGained()
        {
            IsActive = true;
            _disableScreensaver.Invoke();
            WindowGainedFocus();
        }

        private void Window_FocusLost()
        {
            IsActive = false;
            _enableScreensaver.Invoke();
            WindowLostFocus();
        }

        public void Run()
        {
            long totalTicks = 0;
            long previousTicks = Stopwatch.GetTimestamp();

            if (RunBody(ref totalTicks, ref previousTicks))
            {
                Window.Visible = true;

                while (Window.Exists)
                {
                    if (!RunBody(ref totalTicks, ref previousTicks))
                        break;

                    TimeAverager.Tick();
                }
            }

            DisposeGraphicsDeviceObjects();
            GraphicsDevice.Dispose();
        }

        private bool RunBody(ref long totalTicks, ref long previousTicks)
        {
            long currentTicks = Stopwatch.GetTimestamp();
            long deltaTicks = currentTicks - previousTicks;
            previousTicks = currentTicks;
            totalTicks += deltaTicks;

            var time = new FrameTime(
                TimeSpan.FromSeconds(totalTicks * TimeAverager.SecondsPerTick),
                TimeSpan.FromSeconds(deltaTicks * TimeAverager.SecondsPerTick),
                IsActive);

            TimeAverager.BeginUpdate();
            PumpSdlEvents();
            Update(time);
            TimeAverager.EndUpdate();

            if (_shouldExit)
            {
                Window.Close();
                _shouldExit = false;
                return false;
            }

            if (!Window.Exists)
                return false;

            if (time.IsActive)
            {
                DrawAndPresent();
            }
            else
            {
                if (_drawWhenUnfocused)
                {
                    if (Window.WindowState == WindowState.Minimized)
                    {
                        if (_drawWhenMinimized)
                            DrawAndPresent();
                    }
                    else
                    {
                        DrawAndPresent();
                    }
                }

                double spentMillis = (Stopwatch.GetTimestamp() - currentTicks) * TimeAverager.MillisPerTick;
                int millis = (int)(_inactiveFrameTime.TotalMilliseconds - spentMillis);
                if (millis > 0)
                    Thread.Sleep(millis);
            }
            return true;
        }

        public void Exit()
        {
            _shouldExit = true;
        }

        private void PumpSdlEvents()
        {
            Sdl2Events.ProcessEvents();
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
            GraphicsDevice.SwapBuffers();
        }

        private void DrawAndPresent()
        {
            TimeAverager.BeginDraw();
            Draw();
            TimeAverager.EndDraw();

            TimeAverager.BeginPresent();
            Present();
            TimeAverager.EndPresent();
        }

        public void ChangeGraphicsBackend(GraphicsBackend backend, bool forceRecreateWindow)
        {
            GraphicsBackend previousBackend = GraphicsDevice.BackendType;
            bool syncToVBlank = GraphicsDevice.SyncToVerticalBlank;

            DisposeGraphicsDeviceObjects();
            GraphicsDevice.Dispose();

            if (AlwaysRecreateWindow || forceRecreateWindow)
            {
                var windowCI = new WindowCreateInfo
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

            var gdOptions = new GraphicsDeviceOptions(
                ShouldEnableGraphicsDeviceDebug(), null, syncToVBlank, ResourceBindingModel.Improved, true, true, SrgbSwapchain);

            try
            {
                GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions, backend);
            }
            catch
            {
                GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, gdOptions, previousBackend);
            }
            CreateGraphicsDeviceObjects();
        }

        protected virtual bool ShouldEnableGraphicsDeviceDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        protected virtual void WindowResized()
        {
        }

        protected virtual void WindowGainedFocus()
        {
        }

        protected virtual void WindowLostFocus()
        {
        }

        protected void ChangeGraphicsBackend(GraphicsBackend backend)
        {
            ChangeGraphicsBackend(backend, false);
        }

        protected virtual void DisposeGraphicsDeviceObjects()
        {
        }

        protected virtual void CreateGraphicsDeviceObjects()
        {
        }
    }
}
