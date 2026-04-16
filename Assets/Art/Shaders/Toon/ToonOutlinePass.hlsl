#ifndef TOON_OUTLINE_PASS_INCLUDED
#define TOON_OUTLINE_PASS_INCLUDED

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half   fogFactor  : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVertex(OutlineAttributes IN)
{
    OutlineVaryings OUT = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    #ifndef _OUTLINE_ON
        // Collapse geometry to a single point to effectively disable the pass.
        OUT.positionCS = float4(0,0,0,1);
        OUT.uv = IN.uv;
        OUT.fogFactor = 0;
        return OUT;
    #endif

    // Expand along the vertex normal in view space so the outline
    // thickness stays roughly constant on screen.
    float3 positionVS = TransformWorldToView(TransformObjectToWorld(IN.positionOS.xyz));
    float3 normalVS   = TransformWorldToViewDir(TransformObjectToWorldNormal(IN.normalOS));

    // Scale by clip-space w so outline width is roughly uniform at all distances.
    float4 clipPos = TransformWViewToHClip(positionVS);
    float  scale   = clipPos.w * _OutlineWidth * 0.005;

    positionVS.xy += normalize(normalVS.xy) * scale;
    positionVS.z  += _OutlineDepthOffset * 0.01;

    OUT.positionCS = TransformWViewToHClip(positionVS);
    OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
    OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);

    return OUT;
}

half4 OutlineFragment(OutlineVaryings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    #ifndef _OUTLINE_ON
        clip(-1);
    #endif

    half4 col = SampleToonAlbedo(IN.uv);
    half3 rgb = _OutlineColor.rgb;
    rgb = MixFog(rgb, IN.fogFactor);
    return half4(rgb, _OutlineColor.a);
}

#endif
