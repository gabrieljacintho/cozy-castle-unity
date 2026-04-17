#ifndef TOON_INPUT_INCLUDED
#define TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// SurfaceInput.hlsl declares _BaseMap / sampler_BaseMap / _BumpMap / _EmissionMap
// and the Alpha(), SampleAlbedoAlpha() helpers. Do NOT redeclare them.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Single UnityPerMaterial CBUFFER shared by every pass (SRP Batcher requirement).
// The URP ShadowCaster / DepthOnly / DepthNormals passes reference
// _BaseMap_ST, _BaseColor, _Cutoff from this CBUFFER by name.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;

    half4  _ShadowColor;
    half   _ShadowStrength;
    half   _Steps;
    half   _StepSmoothness;
    half   _ShadingOffset;
CBUFFER_END

// Only declare textures NOT already declared by SurfaceInput.hlsl.
TEXTURE2D(_RampTex);        SAMPLER(sampler_RampTex);

// Toon-specific albedo sample that applies our base color and alpha clip.
// URP's built-in Alpha() handles the _Cutoff clip so we just call it.
half4 SampleToonAlbedo(float2 uv)
{
    half4 c = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    c *= _BaseColor;
    #ifdef _ALPHATEST_ON
        clip(c.a - _Cutoff);
    #endif
    return c;
}

#endif
