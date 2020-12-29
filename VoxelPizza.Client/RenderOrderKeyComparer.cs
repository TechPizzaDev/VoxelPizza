using System.Collections.Generic;
using System.Numerics;

namespace VoxelPizza.Client
{
    internal class RenderOrderKeyComparer : IComparer<Renderable>
    {
        public Vector3 CameraPosition { get; set; }

        public int Compare(Renderable? x, Renderable? y)
        {
            if (x == null)
            {
                if (y == null)
                    return 0;
                return -1;
            }
            else if (y == null)
                return 1;

            var xkey = x.GetRenderOrderKey(CameraPosition);
            var ykey = y.GetRenderOrderKey(CameraPosition);
            return xkey.CompareTo(ykey);
        }
    }
}