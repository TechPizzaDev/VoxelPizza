#version 450

layout(set = 0, binding = 0) uniform ProjView
{
    mat4 View;
    mat4 Proj;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 InstancePosition;
layout(location = 2) in vec4 InstanceVelocity;
layout(location = 3) in vec4 InstanceColor;
layout(location = 0) out vec4 _Velocity;
layout(location = 1) out vec4 _Color;

void main()
{
    vec3 transformedPos = Position + InstancePosition.xyz;

    vec4 pos = vec4(transformedPos, 1);

    _Velocity = InstanceVelocity;
    _Color = InstanceColor;
    gl_Position = Proj * View * pos;
}
