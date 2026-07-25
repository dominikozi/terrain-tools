Shader "Terrain Tools/Terrain Surface Mesh Blend"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BaseNormal("Base Normal", 2D) = "bump" {}
        _BaseNormalScale("Base Normal Strength", Range(0, 2)) = 1
        _BaseMask("Base Mask (Metallic R, AO G, Smoothness A)", 2D) = "white" {}
        [ToggleUI] _BaseHasMask("Use Base Mask", Float) = 0
        [Gamma] _BaseMetallic("Base Metallic", Range(0, 1)) = 0
        _BaseSmoothness("Base Smoothness", Range(0, 1)) = 0.5

        [HideInInspector] _TS_AlbedoHeightArray("Albedo Height Array", 2DArray) = "" {}
        [HideInInspector] _TS_NormalSurfaceArray("Normal Surface Array", 2DArray) = "" {}
        [HideInInspector] _TS_MetallicArray("Metallic Array", 2DArray) = "" {}
        [HideInInspector] _TS_DetailNoise("Detail Noise", 2D) = "gray" {}
        [HideInInspector] _TS_MacroNoise("Macro Noise", 2D) = "gray" {}
        [HideInInspector] _TS_NormalNoise("Normal Noise", 2D) = "bump" {}
        [HideInInspector] _TS_GlobalTint("Global Tint", 2D) = "gray" {}
        [HideInInspector] _TS_GlobalNormal("Global Normal", 2D) = "bump" {}
        [HideInInspector] [PerRendererData] _TS_MeshBlendNoise("Blend Noise", 2D) = "gray" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TS_MeshForwardVertex
            #pragma fragment TS_MeshForwardFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_instancing
            #include "TerrainSurfaceMeshBlendPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TS_MeshShadowVertex
            #pragma fragment TS_MeshShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "TerrainSurfaceMeshBlendPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TS_MeshDepthVertex
            #pragma fragment TS_MeshDepthFragment
            #pragma multi_compile_instancing
            #include "TerrainSurfaceMeshBlendPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TS_MeshForwardVertex
            #pragma fragment TS_MeshDepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "TerrainSurfaceMeshBlendPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex TS_MeshMetaVertex
            #pragma fragment TS_MeshMetaFragment
            #pragma multi_compile_instancing
            #include "TerrainSurfaceMeshBlendPasses.hlsl"
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
