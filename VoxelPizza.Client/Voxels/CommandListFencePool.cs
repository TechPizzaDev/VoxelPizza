using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Veldrid;

namespace VoxelPizza.Client
{
    public class CommandListFencePool : GraphicsResource
    {
        private List<CommandListFence> _all = new();
        private ConcurrentStack<CommandListFence> _pool = new();

        public CommandListFencePool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var item = new CommandListFence();
                _all.Add(item);
                _pool.Push(item);
            }
        }

        private void ThrowIsDisposed()
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            foreach (CommandListFence item in _all)
            {
                item.CreateDeviceObjects(gd, cl, sc);
            }
        }

        public override void DestroyDeviceObjects()
        {
            foreach (CommandListFence item in _all)
            {
                item.DestroyDeviceObjects();
            }
        }

        public bool TryRent([MaybeNullWhen(false)] out CommandListFence item)
        {
            if (IsDisposed)
            {
                ThrowIsDisposed();
            }

            return _pool.TryPop(out item);
        }

        public void Return(CommandListFence item)
        {
            if (item == null)
            {
                return;
            }

            if (IsDisposed)
            {
                item.Dispose();
                return;
            }

            _pool.Push(item);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (CommandListFence item in _all)
                {
                    item.Dispose();
                }
                _all.Clear();
                _pool.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
