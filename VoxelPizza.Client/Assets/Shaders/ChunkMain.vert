#version 450

struct TextureAnimation
{
    uint StepType;
    uint StepCount;
    float StepRate;
};

layout(set = 0, binding = 0) uniform ProjectionMatrix
{
    mat4 Projection;
};

layout(set = 0, binding = 1) uniform ViewMatrix
{
    mat4 View;
};

layout(set = 1, binding = 0) uniform ChunkInfo
{
    mat4 World;
    mat4 InverseWorld;
};

layout(location = 0) in vec4 Position;
layout(location = 1) in uint Normal;

layout(location = 2) in ivec4 Color;
layout(location = 3) in uint TexAnimation0;
layout(location = 4) in uint TexRegion0;


layout(location = 0) out vec3 f_Normal;
layout(location = 1) out vec4 f_Color;

layout(location = 2) flat out uint f_TexRegion0;
layout(location = 3) flat out uint f_StepType0;
layout(location = 4) flat out uint f_StepCount0;
layout(location = 5) flat out float f_StepRate0;

vec3 unpack3x10(uint packed) 
{
    vec3 v;
    v.x = (packed & 1023);
    v.y = (packed >> 10) & 1023;
    v.z = (packed >> 20) & 1023;
    v /= 1023.0 / 2.0;
    v -= 1.0;
    return v;
}

TextureAnimation unpackTexAnim(uint packed)
{
    TextureAnimation anim;
    anim.StepType = packed & 7;
    anim.StepCount = (packed >> 3) & 4095;
    anim.StepRate = (packed >> 15) / 4096.0;
    return anim;
}

void main()
{
    vec4 worldPosition = World * Position;
    vec4 outPosition = Projection * View * worldPosition;

    vec3 normal = unpack3x10(Normal);
    vec4 outNormal = InverseWorld * vec4(normal, 1);

    vec4 outColor = Color / 255.0;

    TextureAnimation texAnim0 = unpackTexAnim(TexAnimation0);
    

    gl_Position = outPosition;

    f_Normal = normalize(outNormal.xyz);
    f_Color = outColor;

    f_TexRegion0 = TexRegion0;
    f_StepType0 = texAnim0.StepType;
    f_StepCount0 = texAnim0.StepCount;
    f_StepRate0 = texAnim0.StepRate;

    f_StepCount0 = 3;
    f_StepRate0 = 1;

    if(TexAnimation0 == 1)
    {
        f_StepType0 = 2;
    }
    else
    {
        f_StepType0 = 1;
    }
}
