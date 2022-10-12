#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    float3 position;
    float3 normal;
    float3 viewDirection;
    float3 color;
    float alpha;
    float depth;
    float metallic;
    float smoothness;
    float fresnelStrength;
    float dither; //阴影噪声浮点
};

#endif
