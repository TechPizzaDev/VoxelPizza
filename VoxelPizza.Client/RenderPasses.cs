using System;

namespace VoxelPizza.Client
{
    [Flags]
    public enum RenderPasses : int
    {
        Opaque = 1 << 0,
        AlphaBlend = 1 << 1,
        Overlay = 1 << 2,
        ShadowMapNear = 1 << 3,
        ShadowMapMid = 1 << 4,
        ShadowMapFar = 1 << 5,
        Duplicator = 1 << 6,
        SwapchainOutput = 1 << 7,
        AllShadowMap = ShadowMapNear | ShadowMapMid | ShadowMapFar,
    }
}
