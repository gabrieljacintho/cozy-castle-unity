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

        [Header(Specular)]
        [Toggle(_SPECULAR_ON)] _SpecularOn("Enable Specular", Float) = 1
        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _Glossiness("Glossiness", Range(1, 256)) = 32
        _SpecularThreshold("Specular Threshold", Range(0,1)) = 0.5
        _SpecularSmoothness("Specular Smoothness", Range(0.001, 0.5)) = 0.02

        [Header(Rim)]
        [Toggle(_RIM_ON)] _RimOn("Enable Rim Light", Float) = 1
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimThreshold("Rim Threshold", Range(0,1)) = 0.6
        _RimSmoothness("Rim Smoothness", Range(0.001, 0.5)) = 0.05
        _RimLightAlign("Rim Light Align", Range(0,1)) = 0.2

        [Header(Additional Lights)]
        [Toggle(_ADDITIONAL_LIGHTS_TOON)] _AdditionalLightsToon("Toon Additional Lights", Float) = 1
        _AdditionalLightIntensity("Additional Light Intensity", Range(0, 2)) = 1.0

        [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _OutlineOn("Enable Outline Pass", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 1.0
        _OutlineDepthOffset("Outline Depth Offset", Range(-1, 1)) = 0.0

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
            #pragma shader_feature_local_fragment _SPECULAR_ON
            #pragma shader_feature_local_fragment _RIM_ON
            #pragma shader_feature_local_fragment _ADDITIONAL_LIGHTS_TOON

            #include "ToonInput.hlsl"
            #include "ToonLighting.hlsl"
            #include "ToonForwardPass.hlsl"
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

        // ==============================================================
        //  Outline pass (inverted hull)
        // ==============================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull  Front
            ZWrite On
            ZTest  LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   OutlineVertex
            #pragma fragment OutlineFragment

            #pragma shader_feature_local _OUTLINE_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "ToonInput.hlsl"
            #include "ToonOutlinePass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
