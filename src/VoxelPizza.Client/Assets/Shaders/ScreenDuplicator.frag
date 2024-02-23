#version 320 es
precision highp float;
precision lowp sampler;

layout(set = 0, binding = 0) uniform texture2D SourceTexture;
layout(set = 0, binding = 1) uniform sampler SourceSampler;

layout(location = 0) in vec2 fsin_0;
layout(location = 0) out vec4 _outputColor_0;
layout(location = 1) out vec4 _outputColor_1;

void main()
{
    _outputColor_0 = clamp(texture(sampler2D(SourceTexture, SourceSampler), fsin_0), 0.0, 1.0);
    _outputColor_1 = clamp(texture(sampler2D(SourceTexture, SourceSampler), fsin_0) * vec4(1.0, 0.7, 0.7, 1.0), 0.0, 1.0);
}
