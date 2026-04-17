#ifndef TOON_LIGHTING_INCLUDED
#define TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// URP 17+ (Unity 6) renamed USE_FORWARD_PLUS -> USE_CLUSTER_LIGHT_LOOP.
// Alias the old name so the rest of the code keeps working on either URP version.
// (URP's ForwardPlusKeyword.deprecated.hlsl actually does the reverse alias on
// new versions, so one of these will already be defined — we just guarantee both.)
#if defined(USE_CLUSTER_LIGHT_LOOP) && !defined(USE_FORWARD_PLUS)
    #define USE_FORWARD_PLUS 1
#endif
#if defined(USE_FORWARD_PLUS) && !defined(USE_CLUSTER_LIGHT_LOOP)
    #define USE_CLUSTER_LIGHT_LOOP 1
#endif

// ---------------------------------------------------------------
// Stepped half-lambert quantization.
// ---------------------------------------------------------------
half ToonRamp(half NdotL, half steps, half smoothness, half offset)
{
    half halfLambert = NdotL * 0.5h + 0.5h + offset;
    halfLambert = saturate(halfLambert);

    #ifdef _USE_RAMP
        return SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(halfLambert, 0.5)).r;
    #else
        half stepsClamped = max(1.0h, steps);
        half scaled = halfLambert * stepsClamped;
        half stepped = floor(scaled);
        half frac    = scaled - stepped;
        half smoothed = smoothstep(0.5h - smoothness * stepsClamped,
                                   0.5h + smoothness * stepsClamped,
                                   frac);
        return (stepped + smoothed) / stepsClamped;
    #endif
}

half3 ShadeToonLight(Light light, half3 N, half3 albedo)
{
    half NdotL = dot(N, light.direction);
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    half band  = ToonRamp(NdotL, _Steps, _StepSmoothness, _ShadingOffset);
    half shadowBand = smoothstep(0.3h, 0.7h, atten);
    band = min(band, shadowBand);

    half3 lit    = albedo * light.color;
    half3 shadow = lerp(albedo, albedo * _ShadowColor.rgb, _ShadowStrength);
    half3 diffuse = lerp(shadow, lit, band);

    return diffuse;
}

half3 CalculateToonLighting(InputData inputData, half3 albedo)
{
    half3 N = inputData.normalWS;

    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    #else
        Light mainLight = GetMainLight();
    #endif

    MixRealtimeAndBakedGI(mainLight, N, inputData.bakedGI);

    half3 color = ShadeToonLight(mainLight, N, albedo);

    color += inputData.bakedGI * albedo * 0.3h;

    return color;
}

#endif
