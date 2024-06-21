using System;
using System.Collections.Generic;
using System.IO;
using Veldrid;
using Veldrid.SPIRV;

namespace VoxelPizza.Client.Resources
{
    public static class ShaderHelper
    {
        public static (Shader vs, Shader fs, SpecializationConstant[] specializations) LoadSPIRV(
            GraphicsDevice gd,
            ResourceFactory factory,
            string vertexShaderName,
            string fragmentShaderName,
            ReadOnlySpan<SpecializationConstant> specializations)
        {
            byte[] vsBytes = LoadBytecode(GraphicsBackend.Vulkan, vertexShaderName, ShaderStages.Vertex);
            byte[] fsBytes = LoadBytecode(GraphicsBackend.Vulkan, fragmentShaderName, ShaderStages.Fragment);

            bool debug = false;
#if DEBUG
            debug = true;
#endif
            CrossCompileOptions options = GetOptions(gd, specializations);
            Shader[] shaders = factory.CreateFromSpirv(
                new ShaderDescription(ShaderStages.Vertex, vsBytes, "main", debug),
                new ShaderDescription(ShaderStages.Fragment, fsBytes, "main", debug),
                options);

            Shader vs = shaders[0];
            Shader fs = shaders[1];

            vs.Name = vertexShaderName + "-Vertex";
            fs.Name = fragmentShaderName + "-Fragment";

            return (vs, fs, options.Specializations);
        }

        private static CrossCompileOptions GetOptions(
            GraphicsDevice gd, ReadOnlySpan<SpecializationConstant> specializations)
        {
            SpecializationConstant[] specArray = GetExtendedSpecializations(gd, specializations);

            bool fixClipZ =
                (gd.BackendType == GraphicsBackend.OpenGL ||
                gd.BackendType == GraphicsBackend.OpenGLES)
                && !gd.IsDepthRangeZeroToOne;

            bool invertY = false;

            return new CrossCompileOptions(fixClipZ, invertY, specArray);
        }

        public static SpecializationConstant[] GetExtendedSpecializations(
            GraphicsDevice gd, ReadOnlySpan<SpecializationConstant> specializations)
        {
            List<SpecializationConstant> specs = new(specializations.Length + 4);
            HashSet<uint> usedConstants = new(specializations.Length);

            foreach (SpecializationConstant spec in specializations)
            {
                if (!usedConstants.Add(spec.ID))
                {
                    throw new ArgumentException(
                        $"Provided constants share the same ID ({spec.ID}).",
                        nameof(specializations));
                }
                specs.Add(spec);
            }

            if (!usedConstants.Contains(100))
            {
                specs.Add(new SpecializationConstant(100, gd.IsClipSpaceYInverted));
            }

            if (!usedConstants.Contains(101))
            {
                bool glOrGles = gd.BackendType == GraphicsBackend.OpenGL || gd.BackendType == GraphicsBackend.OpenGLES;
                specs.Add(new SpecializationConstant(101, glOrGles)); // TextureCoordinatesInvertedY
            }

            if (!usedConstants.Contains(102))
            {
                specs.Add(new SpecializationConstant(102, gd.IsDepthRangeZeroToOne));
            }

            if (!usedConstants.Contains(103))
            {
                PixelFormat swapchainFormat = gd.MainSwapchain.Framebuffer.OutputDescription.ColorAttachments[0].Format;
                bool swapchainIsSrgb =
                    swapchainFormat == PixelFormat.B8_G8_R8_A8_UNorm_SRgb ||
                    swapchainFormat == PixelFormat.R8_G8_B8_A8_UNorm_SRgb;
                specs.Add(new SpecializationConstant(103, swapchainIsSrgb));
            }

            return specs.ToArray();
        }

        public static byte[] LoadBytecode(GraphicsBackend backend, string shaderName, ShaderStages stage)
        {
            string stageExt = stage == ShaderStages.Vertex ? "vert" : "frag";
            string name = shaderName + "." + stageExt;

            if (backend == GraphicsBackend.Vulkan ||
                backend == GraphicsBackend.Direct3D11)
            {
                string bytecodeExtension = GetBytecodeExtension(backend);
                string bytecodePath = AssetHelper.GetPath(Path.Combine("Shaders", name + bytecodeExtension));
                if (File.Exists(bytecodePath))
                {
                    return File.ReadAllBytes(bytecodePath);
                }
            }

            string extension = GetSourceExtension(backend);
            string path = AssetHelper.GetPath(Path.Combine("Shaders.Generated", name + extension));
            return File.ReadAllBytes(path);
        }

        private static string GetBytecodeExtension(GraphicsBackend backend)
        {
            return backend switch
            {
                GraphicsBackend.Direct3D11 => ".hlsl.bytes",
                GraphicsBackend.Vulkan => ".spv",
                GraphicsBackend.OpenGL => throw new InvalidOperationException("OpenGL and OpenGLES do not support shader bytecode."),
                _ => throw new InvalidOperationException("Invalid Graphics backend: " + backend),
            };
        }

        private static string GetSourceExtension(GraphicsBackend backend)
        {
            return backend switch
            {
                GraphicsBackend.Direct3D11 => ".hlsl",
                GraphicsBackend.Vulkan => ".450.glsl",
                GraphicsBackend.OpenGL => ".330.glsl",
                GraphicsBackend.OpenGLES => ".300.glsles",
                GraphicsBackend.Metal => ".metallib",
                _ => throw new InvalidOperationException("Invalid Graphics backend: " + backend),
            };
        }
    }
}
