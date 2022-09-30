#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare

//#define SHADOW_SAMPLER s_linear_clamp_sampler 
SAMPLER_CMP(SHADOW_SAMPLER); //常规双线性过滤对深度数据没有意义。

CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
float _ShadowDistance;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int tileIndex;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
};

//Shadows贴图的采样结果决定了在只考虑Shadows的情况下，有多少光线到达表面。
//它是0-1范围内的值，称为衰减因子。
//如果片元被完全遮挡，那么我们得到0，而当它完全没有遮蔽时，我们得到1。
//介于两者之间的值表明片元部分被遮挡。
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(
        _DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
    );
}

float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS)
{
    if (data.strength <= 0.0)
    {
        return 1.0;
    }
    const float3 positionSTS = mul(
        _DirectionalShadowMatrices[data.tileIndex],
        float4(surfaceWS.position, 1.0)
    ).xyz;
    const float shadow = SampleDirectionalShadowAtlas(positionSTS);
    // return shadow;
    return lerp(1.0, shadow, data.strength);
}

float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.strength = surfaceWS.depth < _ShadowDistance ? 1.0 : 0.0;;
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w) //在C#代码中 sphere的w代表球心 这里基本上是比较距离确定此像素对应的点是否在球内
        {
            break;
        }
    }
    if (i == _CascadeCount)
    {
        data.strength = 0.0;
    }
    data.cascadeIndex = i;
    return data;
}


#endif
