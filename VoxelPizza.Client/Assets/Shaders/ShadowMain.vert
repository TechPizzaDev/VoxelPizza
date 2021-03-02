#version 450

layout(set = 0, binding = 0) uniform LightViewProjection1
{
    mat4 _LightViewProjection1;
};

layout(set = 0, binding = 1) uniform LightViewProjection2
{
    mat4 _LightViewProjection2;
};

layout(set = 0, binding = 2) uniform LightViewProjection3
{
    mat4 _LightViewProjection3;
};

layout(set = 0, binding = 5) uniform CameraInfo
{
    mat4 _Projection;
    mat4 _View;
    
    vec4 CameraPosition;
    vec4 CameraLookDirection;
};


layout(set = 1, binding = 0) uniform WorldAndInverse
{
    mat4 World;
    mat4 InverseWorld;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoord;
layout(location = 0) out vec3 fsin_Position_WorldSpace;
layout(location = 1) out vec4 fsin_LightPosition1;
layout(location = 2) out vec4 fsin_LightPosition2;
layout(location = 3) out vec4 fsin_LightPosition3;
layout(location = 4) out vec3 fsin_Normal;
layout(location = 5) out vec2 fsin_TexCoord;
layout(location = 6) out float fsin_FragDepth;

void main()
{
    vec4 worldPosition = World * vec4(Position, 1);
    vec4 viewPosition = _View * worldPosition;
    gl_Position = _Projection * viewPosition;
    fsin_Position_WorldSpace = worldPosition.xyz;
    vec4 outNormal = InverseWorld * vec4(Normal, 1);
    fsin_Normal = normalize(outNormal.xyz);
    fsin_TexCoord = TexCoord;
    fsin_LightPosition1 = World * vec4(Position, 1);
    fsin_LightPosition1 = _LightViewProjection1 * fsin_LightPosition1;
    fsin_LightPosition2 = World * vec4(Position, 1);
    fsin_LightPosition2 = _LightViewProjection2 * fsin_LightPosition2;
    fsin_LightPosition3 = World * vec4(Position, 1);
    fsin_LightPosition3 = _LightViewProjection3 * fsin_LightPosition3;
    fsin_FragDepth = gl_Position.z;
}
