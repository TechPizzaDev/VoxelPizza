using Veldrid;

namespace VoxelPizza.Client
{
    public class CommandListFence : GraphicsResource
    {
        public CommandList CommandList { get; private set; }
        public Fence Fence { get; private set; }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            CommandList = gd.ResourceFactory.CreateCommandList();
            Fence = gd.ResourceFactory.CreateFence(false);
        }

        public override void DestroyDeviceObjects()
        {
            CommandList?.Dispose();
            CommandList = null!;

            Fence?.Dispose();
            Fence = null!;
        }
    }
}
