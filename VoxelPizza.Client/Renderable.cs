using System.Numerics;
using Veldrid;

namespace VoxelPizza.Client
{
    public abstract class Renderable : GraphicsResource
    {
        public virtual RenderPasses RenderPasses => RenderPasses.Standard;

        public abstract RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition);
        public abstract void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass);
        public abstract void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc);
    }
}
