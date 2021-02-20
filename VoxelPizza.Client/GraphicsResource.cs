using System;
using Veldrid;

namespace VoxelPizza.Client
{
    public abstract class GraphicsResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public abstract void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc);

        public abstract void DestroyDeviceObjects();

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (disposing)
                    DestroyDeviceObjects();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
