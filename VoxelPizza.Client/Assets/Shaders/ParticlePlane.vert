#version 450

layout(set = 0, binding = 0) uniform CameraInfo
{
    mat4 Projection;
    mat4 View;
    mat4 InverseView;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 InstanceInitialPosition;
layout(location = 2) in vec4 InstanceColor;
layout(location = 3) in vec4 InstancePosition;
layout(location = 4) in vec4 InstanceVelocity;
layout(location = 5) in vec4 InstanceScale;
layout(location = 6) in float InstanceTime;

layout(location = 0) out vec4 _Velocity;
layout(location = 1) out vec4 _Color;
layout(location = 2) out float _Time;

void main()
{
    mat3 invViewRot = mat3(InverseView);

    vec3 transformedPos = InstancePosition.xyz + invViewRot * Position * InstanceScale.xyz;

    vec4 pos = vec4(transformedPos, 1);

    _Velocity = InstanceVelocity;
    _Color = InstanceColor;
    gl_Position = Projection * View * pos;
}
