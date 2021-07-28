using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.ImageSharp;

namespace VoxelPizza.Client
{
    // Non-thread-safe cache for resources.
    internal static class StaticResourceCache
    {
        private static readonly Dictionary<GraphicsPipelineDescription, Pipeline> s_pipelines = new();
        private static readonly Dictionary<ResourceLayoutDescription, ResourceLayout> s_layouts = new();
        private static readonly Dictionary<ResourceSetDescription, ResourceSet> s_resourceSets = new();
        private static readonly Dictionary<ShaderSetCacheKey, (Shader, Shader, SpecializationConstant[])> s_shaderSets = new();
        private static readonly Dictionary<ImageSharpTexture, Texture> s_textures = new();
        private static readonly Dictionary<Texture, TextureView> s_textureViews = new();

        private static Texture? _pinkTex;

        public static Pipeline GetPipeline(ResourceFactory factory, ref GraphicsPipelineDescription desc)
        {
            if (!s_pipelines.TryGetValue(desc, out Pipeline? p))
            {
                p = factory.CreateGraphicsPipeline(desc);
                s_pipelines.Add(desc, p);
            }

            return p;
        }

        public static ResourceLayout GetResourceLayout(ResourceFactory factory, ResourceLayoutDescription desc)
        {
            if (!s_layouts.TryGetValue(desc, out ResourceLayout? p))
            {
                p = factory.CreateResourceLayout(desc);
                s_layouts.Add(desc, p);
            }

            return p;
        }

        public static (Shader vs, Shader fs, SpecializationConstant[] specs) GetShaders(
            GraphicsDevice gd,
            ResourceFactory factory,
            string vertexShaderName,
            string fragmentShaderName,
            ReadOnlySpan<SpecializationConstant> specializations = default)
        {
            ShaderSetCacheKey cacheKey = new(vertexShaderName, fragmentShaderName, specializations);

            if (!s_shaderSets.TryGetValue(cacheKey, out (Shader vs, Shader fs, SpecializationConstant[]) set))
            {
                set = ShaderHelper.LoadSPIRV(gd, factory, vertexShaderName, fragmentShaderName, specializations);
                s_shaderSets.Add(cacheKey, set);
            }

            return set;
        }

        public static (Shader vs, Shader fs, SpecializationConstant[] specs) GetShaders(
            GraphicsDevice gd,
            ResourceFactory factory,
            string shaderSetName,
            ReadOnlySpan<SpecializationConstant> specializations = default)
        {
            return GetShaders(gd, factory, shaderSetName, shaderSetName, specializations);
        }

        public static void DisposeGraphicsDeviceObjects()
        {
            foreach (KeyValuePair<GraphicsPipelineDescription, Pipeline> kvp in s_pipelines)
            {
                kvp.Value.Dispose();
            }
            s_pipelines.Clear();

            foreach (KeyValuePair<ResourceLayoutDescription, ResourceLayout> kvp in s_layouts)
            {
                kvp.Value.Dispose();
            }
            s_layouts.Clear();

            foreach (KeyValuePair<ShaderSetCacheKey, (Shader, Shader, SpecializationConstant[])> kvp in s_shaderSets)
            {
                kvp.Value.Item1.Dispose();
                kvp.Value.Item2.Dispose();
            }
            s_shaderSets.Clear();

            foreach (KeyValuePair<ImageSharpTexture, Texture> kvp in s_textures)
            {
                kvp.Value.Dispose();
            }
            s_textures.Clear();

            foreach (KeyValuePair<Texture, TextureView> kvp in s_textureViews)
            {
                kvp.Value.Dispose();
            }
            s_textureViews.Clear();

            _pinkTex?.Dispose();
            _pinkTex = null;

            foreach (KeyValuePair<ResourceSetDescription, ResourceSet> kvp in s_resourceSets)
            {
                kvp.Value.Dispose();
            }
            s_resourceSets.Clear();
        }

        internal static Texture GetTexture2D(GraphicsDevice gd, ResourceFactory factory, ImageSharpTexture textureData)
        {
            if (!s_textures.TryGetValue(textureData, out Texture? tex))
            {
                tex = textureData.CreateDeviceTexture(gd, factory);
                s_textures.Add(textureData, tex);
            }

            return tex;
        }

        internal static TextureView GetTextureView(ResourceFactory factory, Texture texture)
        {
            if (!s_textureViews.TryGetValue(texture, out TextureView? view))
            {
                view = factory.CreateTextureView(texture);
                s_textureViews.Add(texture, view);
            }

            return view;
        }

        internal static unsafe Texture GetPinkTexture(GraphicsDevice gd, ResourceFactory factory)
        {
            if (_pinkTex == null)
            {
                RgbaByte pink = RgbaByte.Pink;
                _pinkTex = factory.CreateTexture(TextureDescription.Texture2D(1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
                gd.UpdateTexture(_pinkTex, (IntPtr)(&pink), 4, 0, 0, 0, 1, 1, 1, 0, 0);
            }

            return _pinkTex;
        }

        internal static ResourceSet GetResourceSet(ResourceFactory factory, ResourceSetDescription description)
        {
            if (!s_resourceSets.TryGetValue(description, out ResourceSet? ret))
            {
                ret = factory.CreateResourceSet(description);
                s_resourceSets.Add(description, ret);
            }

            return ret;
        }
    }
}
