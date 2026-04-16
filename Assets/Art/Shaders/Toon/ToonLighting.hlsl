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

half ToonSpecular(half3 N, half3 V, half3 L, half gloss, half threshold, half smoothness)
{
    half3 H = SafeNormalize(L + V);
    half  NdotH = saturate(dot(N, H));
    half  spec  = pow(NdotH, gloss);
    return smoothstep(threshold - smoothness, threshold + smoothness, spec);
}

half ToonRim(half3 N, half3 V, half3 L, half threshold, half smoothness, half lightAlign)
{
    half fresnel = 1.0h - saturate(dot(N, V));
    half align   = saturate(dot(N, L));
    half biased  = fresnel * lerp(1.0h, align, lightAlign);
    return smoothstep(threshold - smoothness, threshold + smoothness, biased);
}

half3 ShadeToonLight(Light light, half3 N, half3 V, half3 albedo, half intensity)
{
    half NdotL = dot(N, light.direction);
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    half band  = ToonRamp(NdotL, _Steps, _StepSmoothness, _ShadingOffset);
    half shadowBand = smoothstep(0.3h, 0.7h, atten);
    band = min(band, shadowBand);

    half3 lit    = albedo * light.color * intensity;
    half3 shadow = lerp(albedo, albedo * _ShadowColor.rgb, _ShadowStrength);
    half3 diffuse = lerp(shadow, lit, band);

    half3 result = diffuse;

    #ifdef _SPECULAR_ON
        half spec = ToonSpecular(N, V, light.direction,
                                 _Glossiness, _SpecularThreshold, _SpecularSmoothness);
        result += _SpecularColor.rgb * light.color * spec * band * intensity;
    #endif

    #ifdef _RIM_ON
        half rim = ToonRim(N, V, light.direction,
                           _RimThreshold, _RimSmoothness, _RimLightAlign);
        result += _RimColor.rgb * rim * band * intensity;
    #endif

    return result;
}

// ---------------------------------------------------------------
// Main entry point. Works under Forward, Forward+ (old _FORWARD_PLUS),
// and Forward+ (new _CLUSTER_LIGHT_LOOP) thanks to URP's LIGHT_LOOP_*
// macros which expand differently based on which keyword is set.
// ---------------------------------------------------------------
half3 CalculateToonLighting(InputData inputData, half3 albedo)
{
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;

    // --- Main light ---
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    #else
        Light mainLight = GetMainLight();
    #endif

    MixRealtimeAndBakedGI(mainLight, N, inputData.bakedGI);

    half3 color = ShadeToonLight(mainLight, N, V, albedo, 1.0h);

    // Subtle ambient contribution (keeps dark side from going pitch black)
    color += inputData.bakedGI * albedo * 0.3h;

    // --- Additional lights ---
    #if defined(_ADDITIONAL_LIGHTS)
        uint pixelLightCount = GetAdditionalLightsCount();

        // Forward+ note: under clustered lighting, directional additional lights
        // are NOT part of GetAdditionalLightsCount() — they live in a separate
        // range. This matches URP's Lit shader behaviour.
        #if USE_FORWARD_PLUS
            // Non-main directional lights in Forward+
            for (uint dirIndex = 0u; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
            {
                Light light = GetAdditionalLight(dirIndex, inputData.positionWS, inputData.shadowMask);
                #ifdef _ADDITIONAL_LIGHTS_TOON
                    color += ShadeToonLight(light, N, V, albedo, _AdditionalLightIntensity);
                #else
                    half3 atten = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                    color += LightingLambert(atten, light.direction, N) * albedo;
                #endif
            }
        #endif

        // Clustered (punctual) lights in Forward+, all lights in classic Forward
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
            #ifdef _ADDITIONAL_LIGHTS_TOON
                color += ShadeToonLight(light, N, V, albedo, _AdditionalLightIntensity);
            #else
                half3 atten = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                color += LightingLambert(atten, light.direction, N) * albedo;
            #endif
        LIGHT_LOOP_END
    #endif

    // Vertex lights (rare path)
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        color += inputData.vertexLighting * albedo;
    #endif

    return color;
}

#endif
