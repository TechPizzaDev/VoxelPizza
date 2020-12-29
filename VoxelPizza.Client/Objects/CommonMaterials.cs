using System.Numerics;
using Veldrid;

namespace VoxelPizza.Client.Objects
{
    public static class CommonMaterials
    {
        public static MaterialPropertyBuffer Brick { get; }
        public static MaterialPropertyBuffer Vase { get; }
        
        static CommonMaterials()
        {
            Brick = new MaterialPropertyBuffer(new MaterialProperties { SpecularIntensity = new Vector3(0.2f), SpecularPower = 10f }) { Name = "Brick" };
            Vase = new MaterialPropertyBuffer(new MaterialProperties { SpecularIntensity = new Vector3(1.0f), SpecularPower = 10f }) { Name = "Vase" };
        }

        public static void CreateGraphicsDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            Brick.CreateDeviceObjects(gd, cl, sc);
            Vase.CreateDeviceObjects(gd, cl, sc);
        }

        public static void UpdateAll(CommandList cl)
        {
            Brick.UpdateChanges(cl);
            Vase.UpdateChanges(cl);
        }

        public static void DisposeGraphicsDeviceObjects()
        {
            Brick.DestroyDeviceObjects();
            Vase.DestroyDeviceObjects();
        }
    }
}
