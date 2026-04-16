Shader "GabrielBertasso/MeshSection/MeshSectionCap"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.2
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _TextureScale("Texture Scale (world units)", Float) = 1.0

        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.35

        // Stencil reference / mask. Declared as properties so we can toggle the
        // filter from C# during debug. Default values match MESH_SECTION_STENCIL_BIT.
        [IntRange] _StencilRef("Stencil Ref", Range(0, 255)) = 64
        [IntRange] _StencilReadMask("Stencil ReadMask", Range(0, 255)) = 64
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comp", Float) = 3

        _MeshSectionAreaIndex("Area Index", Float) = 0
        _MeshSectionDebug("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+100"
            "IgnoreProjector" = "True"
            "ShaderModel" = "4.5"
        }

        Pass
        {
            Name "Cap"
            Tags { "LightMode" = "MeshSectionCap" }

            Cull Front
            ZWrite On
            ZTest LEqual

            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask 0
                Comp [_StencilComp]
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore

            #pragma multi_compile_fog

            #pragma vertex CapVertex
            #pragma fragment CapFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MeshSectionCore.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _TextureScale;
                float  _AmbientStrength;
                float  _StencilRef;
                float  _StencilReadMask;
                float  _StencilComp;
                float  _MeshSectionAreaIndex;
                float  _MeshSectionDebug;
            CBUFFER_END

            struct CapAttributes
            {
                float4 positionOS : POSITION;
            };

            struct CapVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogCoord    : TEXCOORD1;
            };

            CapVaryings CapVertex(CapAttributes input)
            {
                CapVaryings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 CapFragment(CapVaryings input) : SV_Target
            {
                int areaIndex = (int)(_MeshSectionAreaIndex + 0.5);

                if (areaIndex < 0 || areaIndex >= _MeshSectionAreaCount)
                {
                    return half4(1, 0, 1, 1);
                }

                float alpha = _MeshSectionAreaParams[areaIndex].x;
                float capStrength = 1.0 - alpha;

                if (capStrength <= 0.0)
                {
                    discard;
                }

                float3 faceNormalWS = MeshSection_BoxFaceNormal(input.positionWS, areaIndex);
                float2 uv = MeshSection_BoxFaceUV(input.positionWS, areaIndex, _TextureScale)
                            * _BaseMap_ST.xy + _BaseMap_ST.zw;

                int debugMode = (int)(_MeshSectionDebug + 0.5);
                if (debugMode == 1)
                {
                    float3 c = float3(
                        (areaIndex & 1) ? 1.0 : 0.0,
                        (areaIndex & 2) ? 1.0 : 0.0,
                        (areaIndex & 4) ? 1.0 : 0.0);
                    return half4(c, 1);
                }
                if (debugMode == 2)
                {
                    return half4(faceNormalWS * 0.5 + 0.5, 1);
                }
                if (debugMode == 3)
                {
                    return half4(frac(uv), 0, 1);
                }

                float4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 albedo = albedoTex.rgb * _BaseColor.rgb;

                float3 up = abs(faceNormalWS.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 tangentWS = normalize(cross(up, faceNormalWS));
                float3 bitangentWS = cross(faceNormalWS, tangentWS);
                half3 nTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv),
                    _BumpScale);
                half3 normalWS = normalize(nTS.x * tangentWS + nTS.y * bitangentWS + nTS.z * faceNormalWS);

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = albedo * mainLight.color.rgb * NdotL;

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDirWS));
                float specPower = lerp(4.0, 256.0, _Smoothness);
                half3 specular = mainLight.color.rgb * pow(NdotH, specPower) * _Smoothness * 0.5;

                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                if (any(isnan(ambient)) || all(ambient <= 0))
                {
                    ambient = half3(0.3, 0.3, 0.35) * _AmbientStrength;
                }

                half3 color = diffuse + specular + ambient * albedo + _EmissionColor.rgb;
                color = MixFog(color, input.fogCoord);
                color *= capStrength;

                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
