#version 450

layout(set = 0, binding = 2) uniform LightInfo
{
    vec3 LightDirection;
    vec4 LightColor;
    vec4 AmbientColor;
};

layout(location = 0) in vec3 Normal;
layout(location = 1) in vec2 TexCoord0_0;
layout(location = 2) in vec3 TexColor0_0;
layout(location = 3) in vec2 TexCoord0_1;
layout(location = 4) in vec3 TexColor0_1;
layout(location = 5) in float TexFraction_0;

layout(location = 0) out vec4 f_OutputColor;

void main()
{
    vec3 texColor = mix(TexColor0_0, TexColor0_1, TexFraction_0);

    vec4 surfaceColor = vec4(texColor, 1);

    vec3 lightDir = -LightDirection;
    float lightIntensity = clamp(dot(Normal, lightDir), 0, 1);

    vec4 directionalColor = surfaceColor * mix(AmbientColor, LightColor, lightIntensity);

    f_OutputColor = directionalColor;
}
