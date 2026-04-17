Shader "Custom/URP/Toon"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Toon Shading)]
        _ShadowColor("Shadow Tint", Color) = (0.55, 0.6, 0.75, 1)
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0
        _Steps("Shading Steps", Range(1, 8)) = 3
        _StepSmoothness("Step Smoothness", Range(0.001, 0.2)) = 0.02
        _ShadingOffset("Shading Offset", Range(-1, 1)) = 0.0
        [Toggle(_USE_RAMP)] _UseRamp("Use Ramp Texture", Float) = 0
        [NoScaleOffset] _RampTex("Ramp Texture", 2D) = "white" {}


        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Opaque"
            "RenderPipeline"   = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue"            = "Geometry"
            "IgnoreProjector"  = "True"
            "RenderMode"       = "Deferred"
        }

        LOD 300

        // ==============================================================
        //  Forward / Forward+ lit pass
        // ==============================================================
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull   [_Cull]
            ZWrite [_ZWrite]
            ZTest  [_ZTest]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ToonVertex
            #pragma fragment ToonFragment

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            // Forward+ clustered light loop.
            // URP 17+ (Unity 6): _CLUSTER_LIGHT_LOOP
            // Older URP: _FORWARD_PLUS (kept via deprecation shim)
            // Declaring _CLUSTER_LIGHT_LOOP satisfies both (URP's deprecation header
            // maps _FORWARD_PLUS -> _CLUSTER_LIGHT_LOOP internally on new versions).
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Material keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _USE_RAMP

            #include "ToonInput.hlsl"
            #include "ToonLighting.hlsl"
            #include "ToonForwardPass.hlsl"
            ENDHLSL
        }

        // ==============================================================
        //  GBuffer pass for Deferred+
        // ==============================================================
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            Cull   [_Cull]
            ZWrite [_ZWrite]
            ZTest  [_ZTest]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles gles3 glcore
            #pragma vertex   ToonGBufferVertex
            #pragma fragment ToonGBufferFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "ToonInput.hlsl"
            #include "ToonGBufferPass.hlsl"
            ENDHLSL
        }

        // ==============================================================
        //  Shadow caster
        // ==============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            // Note: these URP passes (ShadowCaster, DepthOnly, DepthNormals) already
            // include the LitInput CBUFFER / sampler declarations internally. We only
            // need our own input file for the UniversalForward pass. Adding custom
            // properties here requires redeclaring them in a local CBUFFER.
            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ==============================================================
        //  Depth only
        // ==============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ==============================================================
        //  Depth + normals (for SSAO, decals, etc.)
        // ==============================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
