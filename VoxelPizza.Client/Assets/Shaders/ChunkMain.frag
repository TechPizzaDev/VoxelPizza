#version 450

struct TextureRegion
{
    uint TexId;
    vec3 TexColor;
    vec2 TexCoord;
    vec3 Emission;
};

layout(set = 0, binding = 2) uniform LightInfo
{
    vec3 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
};

layout(set = 0, binding = 3) readonly restrict buffer TextureAtlas
{
    uvec4 TextureRegions[];
};

layout(location = 0) in vec3 Normal;
layout(location = 1) flat in uint TexRegion0_0;
layout(location = 2) flat in uint TexRegion0_1;
layout(location = 3) in float TexFraction_0;

layout(location = 0) out vec4 f_OutputColor;

vec3 unpackUnorm3x8(uint packed)
{
    return vec3(packed & 255, (packed >> 8) & 255, (packed >> 16) & 255) / 255.0;
}

TextureRegion unpackTexRegion(uint index)
{
    uvec4 packed = TextureRegions[index];
    uint textureRgb = packed.x;
    uint xy = packed.y;
    uint emissionRgb = packed.z;

    TextureRegion region;
    region.TexId = textureRgb & 255;
    region.TexColor = unpackUnorm3x8(textureRgb >> 8).xyz;
    region.TexCoord = vec2(xy & 65535, xy >> 16); // * AtlasTexelSize;
    region.Emission = unpackUnorm3x8(emissionRgb);
    return region;
}

void main()
{
    vec3 texColor;
    
    TextureRegion texRegion0 = unpackTexRegion(TexRegion0_0);
    TextureRegion texRegion1 = unpackTexRegion(TexRegion0_1);
    texColor = mix(texRegion0.TexColor, texRegion1.TexColor, TexFraction_0);

    vec4 surfaceColor = vec4(texColor, 1);

    vec3 lightDir = -LightDirection;
    float lightIntensity = clamp(dot(Normal, lightDir), 0, 1);

    vec4 directionalColor = surfaceColor * mix(AmbientColor, LightColor, lightIntensity);

    f_OutputColor = directionalColor;
}
