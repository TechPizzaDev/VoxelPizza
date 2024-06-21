using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;

namespace VoxelPizza.Client.Input
{
    public static class InputTracker
    {
        private static HashSet<Key> _currentlyPressedKeys = new();
        private static HashSet<Key> _newKeysThisFrame = new();

        private static HashSet<MouseButton> _currentlyPressedMouseButtons = new();
        private static HashSet<MouseButton> _newMouseButtonsThisFrame = new();

        public static Vector2 MousePosition;
        public static Vector2 MouseDelta;

        public static InputSnapshot? FrameSnapshot { get; private set; }

        public static bool GetKey(Key key)
        {
            return _currentlyPressedKeys.Contains(key);
        }

        public static bool GetKeyDown(Key key)
        {
            return _newKeysThisFrame.Contains(key);
        }

        public static bool GetMouseButton(MouseButton button)
        {
            return _currentlyPressedMouseButtons.Contains(button);
        }

        public static bool GetMouseButtonDown(MouseButton button)
        {
            return _newMouseButtonsThisFrame.Contains(button);
        }

        public static void UpdateFrameInput(InputSnapshot snapshot, Sdl2Window window)
        {
            FrameSnapshot = snapshot;
            _newKeysThisFrame.Clear();
            _newMouseButtonsThisFrame.Clear();

            MousePosition = snapshot.MousePosition;
            MouseDelta = window.MouseDelta;

            foreach (ref readonly KeyEvent ke in snapshot.KeyEvents)
            {
                if (ke.Down)
                {
                    KeyDown(ke.Physical);
                }
                else
                {
                    KeyUp(ke.Physical);
                }
            }

            foreach (ref readonly MouseButtonEvent me in snapshot.MouseEvents)
            {
                if (me.Down)
                {
                    MouseDown(me.MouseButton);
                }
                else
                {
                    MouseUp(me.MouseButton);
                }
            }

            if (!window.Focused)
            {
                foreach (Key currentKey in _currentlyPressedKeys)
                {
                    KeyUp(currentKey);
                }

                foreach (MouseButton currentMouseButton in _currentlyPressedMouseButtons)
                {
                    MouseUp(currentMouseButton);
                }
            }
        }

        private static void MouseUp(MouseButton mouseButton)
        {
            _currentlyPressedMouseButtons.Remove(mouseButton);
            _newMouseButtonsThisFrame.Remove(mouseButton);
        }

        private static void MouseDown(MouseButton mouseButton)
        {
            if (_currentlyPressedMouseButtons.Add(mouseButton))
            {
                _newMouseButtonsThisFrame.Add(mouseButton);
            }
        }

        private static void KeyUp(Key key)
        {
            _currentlyPressedKeys.Remove(key);
            _newKeysThisFrame.Remove(key);
        }

        private static void KeyDown(Key key)
        {
            if (_currentlyPressedKeys.Add(key))
            {
                _newKeysThisFrame.Add(key);
            }
        }
    }
}
