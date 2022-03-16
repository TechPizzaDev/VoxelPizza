using System.Diagnostics;
using System.Numerics;
using Veldrid;

namespace VoxelPizza.Client
{
    public class ImGuiRenderable : Renderable, IUpdateable
    {
        private ImGuiRenderer? _imGuiRenderer;
        private int _width;
        private int _height;

        public override RenderPasses RenderPasses => RenderPasses.Overlay;

        public ImGuiRenderable(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public void WindowResized(int width, int height)
        {
            _imGuiRenderer?.WindowResized(width, height);
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            if (_imGuiRenderer == null)
                _imGuiRenderer = new ImGuiRenderer(gd, sc.MainSceneFramebuffer.OutputDescription, _width, _height, ColorSpaceHandling.Linear);
            else
                _imGuiRenderer.CreateDeviceResources(gd, sc.MainSceneFramebuffer.OutputDescription, ColorSpaceHandling.Linear);
        }

        public override void DestroyDeviceObjects()
        {
            _imGuiRenderer?.DestroyDeviceObjects();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey(ulong.MaxValue);
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            Debug.Assert(_imGuiRenderer != null);
            Debug.Assert(renderPass == RenderPasses.Overlay);

            _imGuiRenderer.Render(gd, cl);
        }

        public void Update(in UpdateState state)
        {
            Debug.Assert(_imGuiRenderer != null);

            _imGuiRenderer.Update(state.Time.DeltaSeconds, InputTracker.FrameSnapshot);
        }
    }
}
