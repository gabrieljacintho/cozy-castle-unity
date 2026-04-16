#ifndef TOON_FORWARD_PASS_INCLUDED
#define TOON_FORWARD_PASS_INCLUDED

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS           : SV_POSITION;
    float2 uv                   : TEXCOORD0;
    float3 positionWS           : TEXCOORD1;
    float3 normalWS             : TEXCOORD2;
    float4 tangentWS            : TEXCOORD3;    // xyz: tangent, w: sign
    float3 viewDirWS            : TEXCOORD4;

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
    #ifdef DYNAMICLIGHTMAP_ON
        float2 dynamicLightmapUV : TEXCOORD6;
    #endif

    float4 shadowCoord          : TEXCOORD7;
    half   fogFactor            : TEXCOORD8;
    float4 screenPos            : TEXCOORD9;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

    OUT.positionCS  = posInputs.positionCS;
    OUT.positionWS  = posInputs.positionWS;
    OUT.normalWS    = nrmInputs.normalWS;
    OUT.tangentWS   = float4(nrmInputs.tangentWS, IN.tangentOS.w);
    OUT.viewDirWS   = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
    OUT.uv          = TRANSFORM_TEX(IN.texcoord, _BaseMap);
    OUT.screenPos   = ComputeScreenPos(posInputs.positionCS);

    OUT.shadowCoord = GetShadowCoord(posInputs);
    OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

    OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
    OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);
    #ifdef DYNAMICLIGHTMAP_ON
        OUT.dynamicLightmapUV = IN.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif

    return OUT;
}

// Populate URP's InputData so built-in helpers (GI, shadows, Forward+) work.
void BuildInputData(Varyings IN, out InputData data)
{
    data = (InputData)0;
    data.positionWS        = IN.positionWS;
    data.normalWS          = normalize(IN.normalWS);
    data.viewDirectionWS   = SafeNormalize(IN.viewDirWS);

    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        data.shadowCoord = IN.shadowCoord;
    #else
        data.shadowCoord = float4(0,0,0,0);
    #endif

    data.fogCoord          = IN.fogFactor;
    data.vertexLighting    = half3(0,0,0);

    #if defined(DYNAMICLIGHTMAP_ON)
        data.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.dynamicLightmapUV, IN.vertexSH, data.normalWS);
    #else
        data.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, data.normalWS);
    #endif

    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
    data.shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);

    #if defined(DEBUG_DISPLAY)
        data.positionCS = IN.positionCS;
    #endif
}

half4 ToonFragment(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    half4 albedo = SampleToonAlbedo(IN.uv);

    InputData inputData;
    BuildInputData(IN, inputData);

    // Screen-space occlusion (if the renderer feature is enabled)
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
        inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
    #endif

    half3 color = CalculateToonLighting(inputData, albedo.rgb);

    // Apply fog
    color = MixFog(color, inputData.fogCoord);

    return half4(color, albedo.a);
}

#endif
