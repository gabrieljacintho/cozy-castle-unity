Shader "Custom/URP/StylizedWater"
{
    Properties
    {
        [Header(Surface Colors)]
        _ShallowColor    ("Shallow Color",   Color) = (0.3, 0.7, 0.9, 0.85)
        _DeepColor       ("Deep Color",      Color) = (0.02, 0.15, 0.35, 1.0)
        _DepthDistance   ("Depth Fade Distance", Range(0.1, 20)) = 4.0

        [Header(Side View (Cross Section))]
        _SideColor       ("Side Color",      Color) = (0.05, 0.3, 0.5, 0.9)
        _SideTint        ("Side Tint Strength", Range(0,1)) = 0.6
        _VolumeDensity   ("Volume Density",  Range(0, 5)) = 1.2

        [Header(Surface Waves)]
        _NormalMap       ("Normal Map",      2D)     = "bump" {}
        _NormalStrength  ("Normal Strength", Range(0, 2)) = 0.6
        _WaveSpeed       ("Wave Speed (XY L1, ZW L2)", Vector) = (0.03, 0.02, -0.02, 0.04)
        _WaveScale       ("Wave Scale",      Range(0.1, 10)) = 1.0

        [Header(Foam)]
        _FoamColor       ("Foam Color",      Color) = (1,1,1,1)
        _FoamDistance    ("Foam Distance",   Range(0, 3)) = 0.4
        _FoamNoise       ("Foam Noise",      2D)     = "white" {}
        _FoamCutoff      ("Foam Cutoff",     Range(0, 1)) = 0.5
        _FoamSpeed       ("Foam Speed",      Vector) = (0.05, 0.02, 0, 0)

        [Header(Specular and Fresnel)]
        _SpecularColor   ("Specular Color",  Color) = (1,1,1,1)
        _Smoothness      ("Smoothness",      Range(0, 1)) = 0.9
        _FresnelPower    ("Fresnel Power",   Range(0.1, 8)) = 4.0
        _FresnelColor    ("Fresnel Color",   Color) = (0.7, 0.85, 1.0, 1.0)

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // =====================================================================
        // PASSE 1 - SUPERFICIE (FRONT FACES) - topo da agua com ondas
        // Renderiza PRIMEIRO para escrever no depth buffer e ocluir os backfaces
        // que ficarem atras dela.
        // =====================================================================
        Pass
        {
            Name "WaterSurface"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   SurfVert
            #pragma fragment SurfFrag
            #pragma target   3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthDistance;
                float4 _SideColor;
                float  _SideTint;
                float  _VolumeDensity;
                float4 _NormalMap_ST;
                float  _NormalStrength;
                float4 _WaveSpeed;
                float  _WaveScale;
                float4 _FoamColor;
                float  _FoamDistance;
                float4 _FoamNoise_ST;
                float  _FoamCutoff;
                float4 _FoamSpeed;
                float4 _SpecularColor;
                float  _Smoothness;
                float  _FresnelPower;
                float4 _FresnelColor;
                float  _RefractionStrength;
            CBUFFER_END

            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamNoise);  SAMPLER(sampler_FoamNoise);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float4 screenPos  : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
            };

            Varyings SurfVert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;
                OUT.tangentWS  = float4(normInputs.tangentWS, IN.tangentOS.w);
                OUT.uv         = IN.uv;
                OUT.screenPos  = ComputeScreenPos(posInputs.positionCS);
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 SurfFrag (Varyings IN, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // descarta se por algum motivo este fragmento for backface
                // (no passe 1 nao deve acontecer por causa do Cull Back,
                //  mas isto garante se alguem trocar para Cull Off)
                if (!IS_FRONT_VFACE(isFrontFace, true, false))
                    discard;

                // 1) duas camadas de normal map animadas
                float2 uv1 = IN.positionWS.xz * _WaveScale * 0.10 + _Time.y * _WaveSpeed.xy;
                float2 uv2 = IN.positionWS.xz * _WaveScale * 0.17 + _Time.y * _WaveSpeed.zw;

                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                half3 tangentNormal = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));

                float3 bitangent = cross(IN.normalWS, IN.tangentWS.xyz) * IN.tangentWS.w;
                float3x3 TBN = float3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                float3 normalWS = normalize(mul(tangentNormal, TBN));

                // 2) profundidade da cena
                float2 screenUV      = IN.screenPos.xy / IN.screenPos.w;
                float  sceneDepthRaw = SampleSceneDepth(screenUV);
                float  sceneDepthEye = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                float  waterDepthEye = IN.screenPos.w;
                float  depthDiff     = max(0.0, sceneDepthEye - waterDepthEye);

                // 3) cor base por profundidade
                float depthFade01 = saturate(depthDiff / _DepthDistance);
                half3 waterCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFade01);

                // 4) refracao
                float2 refractUV = screenUV + normalWS.xz * _RefractionStrength;
                refractUV = saturate(refractUV);
                half3 sceneCol  = SampleSceneColor(refractUV);
                half3 refracted = lerp(sceneCol, waterCol, saturate(depthFade01 * 1.2));

                // 5) foam
                float foamMask = 1.0 - saturate(depthDiff / max(_FoamDistance, 0.001));
                float2 foamUV  = IN.positionWS.xz * _FoamNoise_ST.xy + _Time.y * _FoamSpeed.xy;
                float  foamTex = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                float  foam    = step(_FoamCutoff, foamMask * foamTex);

                // 6) iluminacao
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainLight  = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float  NdotH   = saturate(dot(normalWS, halfDir));
                float  specExp = exp2(10.0 * _Smoothness + 1.0);
                half3  spec    = _SpecularColor.rgb * pow(NdotH, specExp) * mainLight.color * mainLight.shadowAttenuation;

                float  NdotV      = saturate(dot(normalWS, viewDirWS));
                float  fresnel    = pow(1.0 - NdotV, _FresnelPower);
                half3  fresnelCol = _FresnelColor.rgb * fresnel;

                // 7) luzes adicionais Forward+
                // InputData e necessario porque _CLUSTER_LIGHT_LOOP (Unity 6)
                // referencia inputData.normalizedScreenSpaceUV internamente.
                InputData inputData = (InputData)0;
                inputData.positionWS              = IN.positionWS;
                inputData.normalWS                = normalWS;
                inputData.viewDirectionWS         = viewDirWS;
                inputData.shadowCoord             = float4(0, 0, 0, 0);
                inputData.fogCoord                = IN.fogFactor;
                inputData.vertexLighting          = half3(0, 0, 0);
                inputData.bakedGI                 = half3(0, 0, 0);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask              = half4(1, 1, 1, 1);

                half3 addDiffuse = 0;
                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    float ndl = saturate(dot(normalWS, light.direction));
                    addDiffuse += light.color * light.distanceAttenuation * light.shadowAttenuation * ndl;
                LIGHT_LOOP_END

                // 8) composicao
                half3 finalColor = refracted
                                 + fresnelCol
                                 + spec
                                 + waterCol * addDiffuse * 0.3;

                finalColor = lerp(finalColor, _FoamColor.rgb, foam);
                finalColor = MixFog(finalColor, IN.fogFactor);

                float alpha = lerp(_ShallowColor.a, 1.0, depthFade01);
                alpha = max(alpha, foam);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASSE 2 - LATERAIS (BACK FACES) - corte duro do volume de agua
        // Renderiza DEPOIS, pegando apenas faces traseiras (Cull Front).
        // Como o passe 1 ja escreveu o depth, as laterais so aparecem onde
        // nao ha superficie na frente (ex: olhando pela parede lateral de
        // um aquario/piscina sem topo visivel).
        // =====================================================================
        Pass
        {
            Name "WaterSides"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   SideVert
            #pragma fragment SideFrag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthDistance;
                float4 _SideColor;
                float  _SideTint;
                float  _VolumeDensity;
                float4 _NormalMap_ST;
                float  _NormalStrength;
                float4 _WaveSpeed;
                float  _WaveScale;
                float4 _FoamColor;
                float  _FoamDistance;
                float4 _FoamNoise_ST;
                float  _FoamCutoff;
                float4 _FoamSpeed;
                float4 _SpecularColor;
                float  _Smoothness;
                float  _FresnelPower;
                float4 _FresnelColor;
                float  _RefractionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  localY     : TEXCOORD2;
            };

            Varyings SideVert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                // normal INVERTIDA porque estamos renderizando o backface:
                // queremos iluminar como se a face apontasse para a camera
                OUT.normalWS   = -TransformObjectToWorldNormal(IN.normalOS);
                OUT.localY     = IN.positionOS.y;
                return OUT;
            }

            half4 SideFrag (Varyings IN) : SV_Target
            {
                // gradiente vertical (topo claro, fundo escuro)
                // ajuste aqui se seu pivot/orientacao for diferente
                float depthFactor = saturate(1.0 - IN.localY);

                float absorption = 1.0 - exp(-_VolumeDensity * depthFactor);

                half3 baseCol  = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorption);
                half3 finalCol = lerp(baseCol, _SideColor.rgb, _SideTint);

                Light mainLight = GetMainLight();
                float ndl = saturate(dot(IN.normalWS, mainLight.direction)) * 0.3 + 0.7;
                finalCol *= mainLight.color * ndl;

                return half4(finalCol, _SideColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
