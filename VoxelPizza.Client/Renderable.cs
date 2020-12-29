using System;
using System.Numerics;
using Veldrid;
using Veldrid.Utilities;

namespace VoxelPizza.Client
{
    public abstract class Renderable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public virtual RenderPasses RenderPasses => RenderPasses.Standard;

        public abstract RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition);
        public abstract void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc);
        public abstract void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass);

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

    public abstract class CullRenderable : Renderable
    {
        public abstract BoundingBox BoundingBox { get; }

        public bool Cull(ref BoundingFrustum visibleFrustum)
        {
            return visibleFrustum.Contains(BoundingBox) == ContainmentType.Disjoint;
        }
    }
}
