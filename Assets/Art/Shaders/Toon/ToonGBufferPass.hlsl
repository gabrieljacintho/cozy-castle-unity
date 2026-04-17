#ifndef TOON_GBUFFER_PASS_INCLUDED
#define TOON_GBUFFER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

struct FragmentOutput
{
    half4 GBuffer0 : SV_Target0;
    half4 GBuffer1 : SV_Target1;
    half4 GBuffer2 : SV_Target2;
    half4 GBuffer3 : SV_Target3;
    #ifdef GBUFFER_OPTIONAL_SLOT_1
    GBUFFER_OPTIONAL_SLOT_1_TYPE GBuffer4 : SV_Target4;
    #endif
    #ifdef GBUFFER_OPTIONAL_SLOT_2
    half4 GBuffer5 : SV_Target5;
    #endif
    #ifdef GBUFFER_OPTIONAL_SLOT_3
    half4 GBuffer6 : SV_Target6;
    #endif
};

struct AttributesGBuffer
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsGBuffer
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;
    float4 tangentWS    : TEXCOORD3;
    
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
    
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord : TEXCOORD5;
    #endif
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsGBuffer ToonGBufferVertex(AttributesGBuffer IN)
{
    VaryingsGBuffer OUT = (VaryingsGBuffer)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    
    VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
    
    OUT.positionCS = posInputs.positionCS;
    OUT.positionWS = posInputs.positionWS;
    OUT.normalWS = nrmInputs.normalWS;
    OUT.tangentWS = float4(nrmInputs.tangentWS, IN.tangentOS.w);
    OUT.uv = TRANSFORM_TEX(IN.texcoord, _BaseMap);
    
    OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
    OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);
    
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        OUT.shadowCoord = GetShadowCoord(posInputs);
    #endif
    
    return OUT;
}

FragmentOutput ToonGBufferFragment(VaryingsGBuffer IN)
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    
    half4 albedo = SampleToonAlbedo(IN.uv);
    half3 normalWS = normalize(IN.normalWS);
    
    InputData inputData = (InputData)0;
    inputData.positionWS = IN.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
    
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = IN.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif
    
    inputData.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
    
    BRDFData brdfData = (BRDFData)0;
    brdfData.albedo = albedo.rgb;
    brdfData.diffuse = albedo.rgb;
    brdfData.specular = half3(0.0h, 0.0h, 0.0h);
    brdfData.reflectivity = 0.0h;
    brdfData.perceptualRoughness = 1.0h;
    brdfData.roughness = 1.0h;
    brdfData.roughness2 = 1.0h;
    brdfData.grazingTerm = 0.0h;
    brdfData.normalizationTerm = 6.0h;
    brdfData.roughness2MinusOne = 0.0h;
    
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);
    half3 color = GlobalIllumination(brdfData, inputData.bakedGI, 1.0h, inputData.normalWS, inputData.viewDirectionWS);
    
    uint materialFlags = kMaterialFlagSpecularHighlightsOff;
    
    #ifdef _RECEIVE_SHADOWS_OFF
        materialFlags |= kMaterialFlagReceiveShadowsOff;
    #endif
    
    return BRDFDataToGbuffer(brdfData, inputData, 0.0h, color, materialFlags);
}

#endif
