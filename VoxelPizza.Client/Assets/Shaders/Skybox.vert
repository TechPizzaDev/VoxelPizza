#version 320 es

layout(set = 1, binding = 0) uniform CameraInfo
{
    mat4 Projection;
    mat4 View;

    vec4 CameraPosition;
    vec4 CameraLookDirection;
};

layout(location = 0) in vec3 vsin_Position;
layout(location = 0) out vec3 fsin_0;

layout(constant_id = 102) const bool ReverseDepthRange = true;

void main()
{
    mat4 view3x3 = mat4(
        View[0][0], View[0][1], View[0][2], 0,
        View[1][0], View[1][1], View[1][2], 0,
        View[2][0], View[2][1], View[2][2], 0,
        0, 0, 0, 1);
    vec4 pos = Projection * view3x3 * vec4(vsin_Position, 1.0);
    gl_Position = vec4(pos.x, pos.y, pos.w, pos.w);
    if (ReverseDepthRange) { gl_Position.z = 0.0; }
    fsin_0 = vsin_Position;
}
