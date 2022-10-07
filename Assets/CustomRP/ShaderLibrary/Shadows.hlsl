#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3

#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5

#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7

#endif


#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare

//#define SHADOW_SAMPLER s_linear_clamp_sampler 
SAMPLER_CMP(SHADOW_SAMPLER); //常规双线性过滤对深度数据没有意义。

CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _CascadeData[MAX_CASCADE_COUNT]; // _CascadeData[i].x= 1/(r*r) y = 纹素长度
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
//float _ShadowDistance;
float4 _ShadowAtlasSize;
float4 _ShadowDistanceFade;
CBUFFER_END


struct ShadowMask
{
    bool distance;
    float4 shadows;
};

struct DirectionalShadowData
{
    float strength; //阴影强度
    int tileIndex; //由层级和光源index换算出的具体采样shadowmap中的哪个贴图
    float normalBias;
};

struct ShadowData
{
    int cascadeIndex; //层级数
    float cascadeBlend; //级联混合
    float strength; //阴影强度
    ShadowMask shadowMask;
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

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP) //如果需要多次采样shadow
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx; //xy为xy的纹素大小 yz为纹理总大小 （因为是正方形所以看起来重复）
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SampleDirectionalShadowAtlas(
            float3(positions[i].xy, positionSTS.z));
    }
    return shadow;
   

    #else //否则只采样一次shadowMap
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}


float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif
    if (directional.strength <= 0.0)
    {
        return 1.0;
    }
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)
    ).xyz;
    float shadow = FilterDirectionalShadow(positionSTS);

    // return shadow;
    if (global.cascadeBlend < 1.0)
    {
        //在中间区域需要采集两个相邻的级联并插值得到最终值使变化不过于突兀
        normalBias = surfaceWS.normal *
            (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex + 1],
            float4(surfaceWS.position + normalBias, 1.0)
        ).xyz;
        shadow = lerp(
            FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return lerp(1.0, shadow, directional.strength);
}

float FadedShadowStrength(float distance, float scale, float fade)
{
    //(1 - d / m) / f 此时m和f已经在cup上被计算为是倒数 所以使用乘法
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;

    data.cascadeBlend = 1.0;

    data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w) //在C#代码中 sphere的w代表半径 这里基本上是比较距离确定此像素对应的点是否在球内
        {
            float fade = FadedShadowStrength( //Culling Sphere
                distanceSqr, _CascadeData[i].x, // _CascadeData[i].x= 1/(r*r)
                _ShadowDistanceFade.z //此时_ShadowDistanceFade.z = 1 - square(1 - f) 
            ); //填入后相当于(1 - square(d) / square(r) ) / f

            if (i == _CascadeCount - 1) //判断是否为最终的级联
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    if (i == _CascadeCount) //如果超出了最终的级联则不应该有阴影
    {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither)
    {
        i += 1;
    }
    #endif

    #if !defined(_CASCADE_BLEND_SOFT)
    data.cascadeBlend = 1.0;
    #endif

    data.cascadeIndex = i;
    return data;
}


#endif
