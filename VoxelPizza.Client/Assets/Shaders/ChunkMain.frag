#version 450

const uint Animation_None = 0x00000000u;
const uint Animation_Flat = 0x00000001u;
const uint Animation_Mix = 0x00000002u;
const uint Animation_ScrollVertical = 0x00000004u;
const uint Animation_ScrollHorizontal = 0x00000008u;

struct PackedTextureRegion
{
    uint TextureAndRgb;
    uint XY;
};

struct TextureRegion
{
    uint TexId;
    vec3 TexColor;
    vec2 TexCoord;
};

layout(set = 0, binding = 2) uniform LightInfo
{
    vec3 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
};

layout(set = 0, binding = 3) uniform WorldInfo
{
    float GlobalTime;
    float GlobalTimeFraction;
};

layout(set = 0, binding = 4) buffer TextureAtlas
{
    vec2 AtlasTexelSize;
    PackedTextureRegion TextureRegions[];
};

layout(location = 0) in vec3 Normal;
layout(location = 1) in vec4 Color;

layout(location = 2) flat in uint TexRegion0;
layout(location = 3) flat in uint StepType0;
layout(location = 4) flat in uint StepCount0;
layout(location = 5) flat in float StepRate0;

layout(location = 0) out vec4 f_OutputColor;

TextureRegion unpackTexRegion(PackedTextureRegion packed)
{
    TextureRegion region;
    region.TexId = packed.TextureAndRgb & 255;
    region.TexColor = unpackUnorm4x8(packed.TextureAndRgb >> 8).xyz;
    region.TexCoord = vec2(packed.XY & 65535, packed.XY >> 16) * AtlasTexelSize;
    return region; 
}

void main()
{
    PackedTextureRegion tmpReg0;
    tmpReg0.XY = 0;
    tmpReg0.TextureAndRgb = 255 << 8 | 100 << 16 | 100 << 24;
    
    PackedTextureRegion tmpReg1;
    tmpReg1.XY = 0;
    tmpReg1.TextureAndRgb = 100 << 8 | 100 << 16 | 255 << 24;
    
    PackedTextureRegion tmpReg2;
    tmpReg2.XY = 0;
    tmpReg2.TextureAndRgb = 100 << 8 | 255 << 16 | 100 << 24;

    TextureRegion texRegions[3];
    texRegions[0] = unpackTexRegion(tmpReg0); // unpackTexRegion(TextureRegions[TexRegion0]);
    texRegions[1] = unpackTexRegion(tmpReg1);
    texRegions[2] = unpackTexRegion(tmpReg2);
    
    float currentStep = GlobalTime * StepRate0;
    vec3 texColor;

    if(StepType0 == Animation_None)
    {
        texColor = texRegions[0].TexColor;
    }
    else if(StepType0 == Animation_Flat)
    {
        float regionIndexF = round(currentStep); 
        uint regionIndex = uint(regionIndexF) % StepCount0;
        texColor = texRegions[regionIndex].TexColor;
    }
    else if(StepType0 == Animation_Mix)
    {
        float regionIndexF; 
        float fraction = modf(currentStep, regionIndexF);
        uint regionIndex = uint(regionIndexF);
        uint regionIndex0 = regionIndex % StepCount0;
        uint regionIndex1 = (regionIndex + 1) % StepCount0;
        texColor = mix(texRegions[regionIndex0].TexColor, texRegions[regionIndex1].TexColor, fraction);
    }

    vec4 surfaceColor = Color * vec4(texColor, 1);

    vec3 lightDir = -LightDirection;
    float lightIntensity = clamp(dot(Normal, lightDir), 0, 1);

    vec4 directionalColor = surfaceColor * mix(AmbientColor, LightColor, lightIntensity);

    f_OutputColor = directionalColor;
}
