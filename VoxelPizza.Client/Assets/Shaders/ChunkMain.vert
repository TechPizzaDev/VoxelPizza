#version 320 es

struct TextureAnimation
{
    uint Type;
    uint Count;
    float Rate;
};

struct TextureRegion
{
    uint TexId;
    vec3 TexColor;
    vec2 TexCoord;
    vec3 Emission;
};

layout(set = 0, binding = 0) uniform CameraInfo
{
    mat4 Projection;
    mat4 View;
    mat4 InverseView;
    mat4 ProjectionView;

    vec4 CameraPosition;
    vec4 CameraLookDirection;
};

layout(set = 1, binding = 0) uniform WorldInfo
{
    float GlobalTime;
};

layout(set = 1, binding = 2) readonly restrict buffer TextureAtlas
{
    uvec4 TextureRegions[];
};

layout(set = 2, binding = 0) uniform ChunkInfo
{
    vec4 Translation;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in uint Normal;
//layout(location = 2) in uint TexAnimation0;
//layout(location = 3) in uint TexRegion0;

layout(location = 0) out vec3 f_Normal;
layout(location = 1) out vec2 f_TexCoord0_0;
layout(location = 2) out vec3 f_TexColor0_0;
layout(location = 3) out vec2 f_TexCoord0_1;
layout(location = 4) out vec3 f_TexColor0_1;
layout(location = 5) out float f_TexFraction_0;

vec3 unpack3x10(uint packed) 
{
    vec3 v;
    v.x = float((packed & 1023u));
    v.y = float((packed >> 10) & 1023u);
    v.z = float((packed >> 20) & 1023u);
    v /= 1023.0 / 2.0;
    v -= 1.0;
    return v;
}

TextureAnimation unpackTexAnim(uint packed)
{
    TextureAnimation anim; 
    anim.Type = packed & 1u;
    anim.Count = (packed >> 1) & 16383u;
    anim.Rate = float(packed >> 15) / 4096.0;
    return anim;
}

vec3 unpackUnorm3x8(uint packed)
{
    return vec3(packed & 255u, (packed >> 8) & 255u, (packed >> 16) & 255u) / 255.0;
}

TextureRegion unpackTexRegion(uint index)
{
    uvec4 packed = TextureRegions[index];
    uint textureRgb = packed.x;
    uint xy = packed.y;
    uint emissionRgb = packed.z;

    TextureRegion region;
    region.TexId = textureRgb & 255u;
    region.TexColor = unpackUnorm3x8(textureRgb >> 8).xyz;
    region.TexCoord = vec2(xy & 65535u, xy >> 16); // * AtlasTexelSize;
    region.Emission = unpackUnorm3x8(emissionRgb);
    return region;
}


void main()
{
    vec4 worldPosition = vec4(Position + Translation.xyz, 1);
    vec4 outPosition = ProjectionView * worldPosition;
    
    mat4 world = mat4(
        vec4(1, 0, 0, Translation.x),
        vec4(0, 1, 0, Translation.y),
        vec4(0, 0, 1, Translation.z),
        vec4(0, 0, 0, 1));
    mat4 inverseWorld = inverse(world);
    vec3 normal = unpack3x10(Normal);
    vec4 outNormal = inverseWorld * vec4(normal, 1);
    
    //uint TexAnimation0 = 0u;
    //uint TexRegion0 = 0u;
    //TextureAnimation texAnim0 = unpackTexAnim(TexAnimation0);
    //float step0 = GlobalTime * texAnim0.Rate;
    //float indexF0;
    //float texFract0 = modf(step0 - float(texAnim0.Type) * 0.5f, indexF0);
    //uint indexOffset0 = uint(indexF0);
    //uint indexOffset0_0 = indexOffset0 % texAnim0.Count;
    //uint indexOffset0_1 = (indexOffset0 + texAnim0.Type) % texAnim0.Count;
    //
    //TextureRegion texRegion0_0 = unpackTexRegion(TexRegion0 + indexOffset0_0);
    //TextureRegion texRegion0_1 = unpackTexRegion(TexRegion0 + indexOffset0_1);
    
    gl_Position = outPosition;

    f_Normal = normalize(outNormal.xyz);
    
    f_TexCoord0_0 = vec2(0, 0); //texRegion0_0.TexCoord;
    f_TexColor0_0 = vec3(0, 0, 0); //texRegion0_0.TexColor;
    f_TexCoord0_1 = vec2(0, 0); //texRegion0_1.TexCoord;
    f_TexColor0_1 = vec3(0, 0, 0); //texRegion0_1.TexColor;
    f_TexFraction_0 = 0.0; //texFract0;
}
