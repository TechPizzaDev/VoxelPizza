using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;
using VoxelPizza.Client.Input;

namespace VoxelPizza.Client
{
    public class Camera : IUpdateable
    {
        private float _fov = 1f;
        private float _near = 0.1f;
        private float _far = 5000f;

        private Matrix4x4 _viewMatrix;
        private Matrix4x4 _inverseViewMatrix;
        private Matrix4x4 _projectionMatrix;
        private Matrix4x4 _inverseProjectionMatrix;

        private Vector3 _position = new(0, 3, 0);
        private Vector3 _lookDirection = new(0, -.3f, -1f);
        private float _moveSpeed = 10.0f;

        private float _yaw;
        private float _pitch;

        private Vector2 _mousePressedPos;
        private bool _mousePressed;
        private bool _invertedClipSpaceY;
        private bool _useReverseDepth;
        private float _windowWidth;
        private float _windowHeight;
        private Sdl2Window _window;

        public event Action<Camera>? ProjectionChanged;
        public event Action<Camera>? ViewChanged;

        public Camera(GraphicsDevice gd, Sdl2Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));

            UpdateGraphicsBackend(gd, window);
            UpdateViewMatrix();
        }

        public void UpdateGraphicsBackend(GraphicsDevice gd, Sdl2Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));

            _invertedClipSpaceY = gd.IsClipSpaceYInverted;
            _useReverseDepth = gd.IsDepthRangeZeroToOne;
            _windowWidth = window.Width;
            _windowHeight = window.Height;
            _window.FocusLost += Window_FocusLost;

            UpdatePerspectiveMatrix();
        }

        private void Window_FocusLost()
        {
            ReleaseMouseGrab();
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 InverseViewMatrix => _inverseViewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        public Vector3 Position { get => _position; set { _position = value; UpdateViewMatrix(); } }
        public Vector3 LookDirection => _lookDirection;

        public float FarDistance => _far;

        public float FieldOfView => _fov;
        public float NearDistance => _near;

        public float ViewWidth => _windowWidth;
        public float ViewHeight => _windowHeight;
        public float AspectRatio => _windowWidth / _windowHeight;

        public float Yaw { get => _yaw; set { _yaw = value; UpdateViewMatrix(); } }
        public float Pitch { get => _pitch; set { _pitch = value; UpdateViewMatrix(); } }

        public Sdl2ControllerTracker? Controller { get; set; }

        public void Update(in UpdateState state)
        {
            float deltaSeconds = state.Time.DeltaSeconds;

            float sprintFactor = 2.5f;
            if (InputTracker.GetKey(Key.LeftControl))
            {
                sprintFactor = 0.5f;
            }
            else
            {
                if (InputTracker.GetKey(Key.LeftShift))
                    sprintFactor += 7.5f;
                if (InputTracker.GetKey(Key.Space))
                    sprintFactor += 125f;
            }

            Vector3 motionDir = Vector3.Zero;

            if (InputTracker.GetKey(Key.A))
            {
                motionDir += -Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.D))
            {
                motionDir += Vector3.UnitX;
            }
            if (InputTracker.GetKey(Key.W))
            {
                motionDir += -Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.S))
            {
                motionDir += Vector3.UnitZ;
            }
            if (InputTracker.GetKey(Key.Q))
            {
                motionDir += -Vector3.UnitY;
            }
            if (InputTracker.GetKey(Key.E))
            {
                motionDir += Vector3.UnitY;
            }

            if (InputTracker.GetKeyDown(Key.F))
            {
                _position += new Vector3(1024, 0, 0);
            }

            if (Controller != null)
            {
                float controllerLeftX = Controller.GetAxis(SDL_GameControllerAxis.LeftX);
                float controllerLeftY = Controller.GetAxis(SDL_GameControllerAxis.LeftY);
                float controllerTriggerL = Controller.GetAxis(SDL_GameControllerAxis.TriggerLeft);
                float controllerTriggerR = Controller.GetAxis(SDL_GameControllerAxis.TriggerRight);

                if (MathF.Abs(controllerLeftX) > 0.2f)
                {
                    motionDir += controllerLeftX * Vector3.UnitX;
                }
                if (MathF.Abs(controllerLeftY) > 0.2f)
                {
                    motionDir += controllerLeftY * Vector3.UnitZ;
                }
                if (controllerTriggerL > 0f)
                {
                    motionDir += controllerTriggerL * -Vector3.UnitY;
                }
                if (controllerTriggerR > 0f)
                {
                    motionDir += controllerTriggerR * Vector3.UnitY;
                }
            }
            if (motionDir != Vector3.Zero)
            {
                Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
                motionDir = Vector3.Transform(motionDir, lookRotation);
                _position += motionDir * _moveSpeed * sprintFactor * deltaSeconds;
                UpdateViewMatrix();
            }

            if (!ImGui.GetIO().WantCaptureMouse &&
                (InputTracker.GetMouseButton(MouseButton.Left) || InputTracker.GetMouseButton(MouseButton.Right)))
            {
                if (!_mousePressed)
                {
                    _mousePressed = true;
                    _mousePressedPos = InputTracker.MousePosition;

                    Sdl2Native.SDL_SetRelativeMouseMode(true);
                    Sdl2Native.SDL_SetWindowGrab(_window.SdlWindowHandle, true);
                }

                Vector2 mouseDelta = InputTracker.MouseDelta;
                Yaw -= mouseDelta.X * 0.002f;
                Pitch -= mouseDelta.Y * 0.002f;
            }
            else if (_mousePressed)
            {
                ReleaseMouseGrab();
                Sdl2Native.SDL_WarpMouseInWindow(_window.SdlWindowHandle, (int)_mousePressedPos.X, (int)_mousePressedPos.Y);
                _mousePressed = false;
            }

            if (Controller != null)
            {
                float controllerRightX = Controller.GetAxis(SDL_GameControllerAxis.RightX);
                float controllerRightY = Controller.GetAxis(SDL_GameControllerAxis.RightY);
                if (MathF.Abs(controllerRightX) > 0.2f)
                {
                    Yaw += -controllerRightX * deltaSeconds;
                }
                if (MathF.Abs(controllerRightY) > 0.2f)
                {
                    Pitch += -controllerRightY * deltaSeconds;
                }
            }

            Pitch = Math.Clamp(Pitch, -1.55f, 1.55f);
            UpdateViewMatrix();
        }

        private void ReleaseMouseGrab()
        {
            Sdl2Native.SDL_SetRelativeMouseMode(false);
            Sdl2Native.SDL_SetWindowGrab(_window.SdlWindowHandle, false);
        }

        public void WindowResized(float width, float height)
        {
            _windowWidth = width;
            _windowHeight = height;
            UpdatePerspectiveMatrix();
        }

        private void UpdatePerspectiveMatrix()
        {
            _projectionMatrix = Util.CreatePerspective(
                _invertedClipSpaceY,
                _useReverseDepth,
                _fov,
                _windowWidth / _windowHeight,
                _near,
                _far);
            Matrix4x4.Invert(_projectionMatrix, out _inverseProjectionMatrix);
            ProjectionChanged?.Invoke(this);
        }

        private void UpdateViewMatrix()
        {
            Quaternion lookRotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
            Vector3 lookDir = Vector3.Transform(-Vector3.UnitZ, lookRotation);
            _lookDirection = lookDir;
            _viewMatrix = Matrix4x4.CreateLookAt(_position, _position + _lookDirection, Vector3.UnitY);
            Matrix4x4.Invert(_viewMatrix, out _inverseViewMatrix);
            ViewChanged?.Invoke(this);
        }

        public Vector3 ScreenToWorld(Vector2 position)
        {
            float x = (2f * position.X) / _windowWidth - 1f;
            float y = 1f - (2f * position.Y) / _windowHeight;
            Vector4 rayClip = new(x, y, -1f, 1f);

            Vector4 rayEye = Vector4.Transform(rayClip, _inverseProjectionMatrix);
            rayEye.Z = -1f;
            rayEye.W = 0f;

            Vector4 rayDirection = Vector4.Transform(rayEye, _inverseViewMatrix);
            rayDirection = Vector4.Normalize(rayDirection);

            return new Vector3(rayDirection.X, rayDirection.Y, rayDirection.Z);
        }

        public CameraInfo GetCameraInfo() => new()
        {
            Projection = _projectionMatrix,
            View = _viewMatrix,
            InverseView = _inverseViewMatrix,
            ProjectionView = _viewMatrix * _projectionMatrix,
            CameraPosition = new Vector4(_position, 0),
            CameraLookDirection = new Vector4(_lookDirection, 0)
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraInfo
    {
        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 InverseView;
        public Matrix4x4 ProjectionView;

        public Vector4 CameraPosition;
        public Vector4 CameraLookDirection;
    }
}
