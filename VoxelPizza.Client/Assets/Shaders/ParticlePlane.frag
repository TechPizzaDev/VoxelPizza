#version 450

layout(location = 0) in vec4 Velocity;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 _OutputColor;

void main()
{
    float o = Velocity.y / 100.0;
    _OutputColor = Color + vec4(0, o, -o, 0);
}
