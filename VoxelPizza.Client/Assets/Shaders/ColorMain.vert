#version 320 es

layout(set = 0, binding = 0) uniform CameraInfo
{
    mat4 Projection;
    mat4 View;

    vec4 CameraPosition;
    vec4 CameraLookDirection;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 f_Color;

void main()
{
    mat4 vp = Projection * View;
    vec4 pos = vp * vec4(Position, 1);

    f_Color = vec4(Color);
    gl_Position = pos;
}
