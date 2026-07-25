#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_MESH_BLEND_PASSES_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_MESH_BLEND_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "TerrainSurfaceMeshBlendSampling.hlsl"

struct TS_MeshAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TS_MeshVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 geometricNormalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half4 color : TEXCOORD4;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
    #ifdef DYNAMICLIGHTMAP_ON
        float2 dynamicLightmapUV : TEXCOORD6;
    #endif
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight : TEXCOORD7;
    #else
        half fogFactor : TEXCOORD7;
    #endif
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord : TEXCOORD8;
    #endif
    #ifdef USE_APV_PROBE_OCCLUSION
        float4 probeOcclusion : TEXCOORD9;
    #endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TS_MeshVaryings TS_MeshForwardVertex(TS_MeshAttributes input)
{
    TS_MeshVaryings output = (TS_MeshVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWS = positionInputs.positionWS;
    output.geometricNormalWS = normalInputs.normalWS;
    output.tangentWS = half4(
        normalInputs.tangentWS,
        input.tangentOS.w * GetOddNegativeScale());
    output.color = input.color;
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    #ifdef DYNAMICLIGHTMAP_ON
        output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif
    OUTPUT_SH4(
        output.positionWS,
        output.geometricNormalWS,
        GetWorldSpaceNormalizeViewDir(output.positionWS),
        output.vertexSH,
        output.probeOcclusion);

    half fogFactor = 0.0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
    #endif
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        output.fogFactorAndVertexLight = half4(
            fogFactor,
            VertexLighting(output.positionWS, output.geometricNormalWS));
    #else
        output.fogFactor = fogFactor;
    #endif
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        output.shadowCoord = GetShadowCoord(positionInputs);
    #endif
    output.positionCS = positionInputs.positionCS;
    return output;
}

void TS_InitializeMeshInputData(TS_MeshVaryings input, float3 normalWS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #else
        inputData.shadowCoord = 0.0;
    #endif
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
        inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    #else
        inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    #endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(_SCREEN_SPACE_IRRADIANCE)
        inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
    #elif defined(DYNAMICLIGHTMAP_ON)
        inputData.bakedGI = SAMPLE_GI(
            input.staticLightmapUV,
            input.dynamicLightmapUV,
            input.vertexSH,
            inputData.normalWS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
        inputData.bakedGI = SAMPLE_GI(
            input.vertexSH,
            GetAbsolutePositionWS(input.positionWS),
            inputData.normalWS,
            inputData.viewDirectionWS,
            input.positionCS.xy,
            input.probeOcclusion,
            inputData.shadowMask);
    #else
        inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

TS_MeshSurface TS_EvaluateMeshSurface(TS_MeshVaryings input)
{
    return TS_BuildMeshSurface(
        input.uv,
        input.color,
        input.positionWS,
        NormalizeNormalPerPixel(input.geometricNormalWS),
        normalize(input.tangentWS.xyz),
        input.tangentWS.w);
}

void TS_MeshForwardFragment(
    TS_MeshVaryings input,
    out half4 outColor : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TS_MeshSurface surface = TS_EvaluateMeshSurface(input);
    InputData inputData;
    TS_InitializeMeshInputData(input, surface.normalWS, inputData);
    #if defined(_DBUFFER)
        half3 specular = 0.0;
        ApplyDecal(
            input.positionCS,
            surface.albedo,
            specular,
            inputData.normalWS,
            surface.metallic,
            surface.occlusion,
            surface.smoothness);
    #endif
    half4 color = UniversalFragmentPBR(
        inputData,
        surface.albedo,
        surface.metallic,
        half3(0.0, 0.0, 0.0),
        surface.smoothness,
        surface.occlusion,
        half3(0.0, 0.0, 0.0),
        1.0);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = half4(color.rgb, 1.0);
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

void TS_MeshDepthNormalsFragment(
    TS_MeshVaryings input,
    out half4 outNormalWS : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    TS_MeshSurface surface = TS_EvaluateMeshSurface(input);
    outNormalWS = half4(NormalizeNormalPerPixel(surface.normalWS), 0.0);
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

struct TS_MeshLeanVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

TS_MeshLeanVaryings TS_MeshDepthVertex(TS_MeshAttributes input)
{
    TS_MeshLeanVaryings output = (TS_MeshLeanVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 TS_MeshDepthFragment(TS_MeshLeanVaryings input) : SV_Target
{
    return input.positionCS.z;
}

float3 _LightDirection;
float3 _LightPosition;

TS_MeshLeanVaryings TS_MeshShadowVertex(TS_MeshAttributes input)
{
    TS_MeshLeanVaryings output = (TS_MeshLeanVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return output;
}

half4 TS_MeshShadowFragment(TS_MeshLeanVaryings input) : SV_Target
{
    return 0.0;
}

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

struct TS_MeshMetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 geometricNormalWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
    float4 color : TEXCOORD4;
};

TS_MeshMetaVaryings TS_MeshMetaVertex(TS_MeshAttributes input)
{
    TS_MeshMetaVaryings output = (TS_MeshMetaVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = UnityMetaVertexPosition(
        input.positionOS.xyz,
        input.staticLightmapUV,
        input.dynamicLightmapUV);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.geometricNormalWS = normalInputs.normalWS;
    output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.color = input.color;
    return output;
}

half4 TS_MeshMetaFragment(TS_MeshMetaVaryings input) : SV_Target
{
    float2 uv = input.uv;
    float4 color = input.color;
    float3 positionWS = input.positionWS;
    float3 geometricNormalWS = NormalizeNormalPerPixel(input.geometricNormalWS);
    float3 tangentWS = normalize(input.tangentWS.xyz);
    float tangentSign = input.tangentWS.w;
    TS_MeshSurface surface = TS_BuildMeshSurface(
        uv,
        color,
        positionWS,
        geometricNormalWS,
        tangentWS,
        tangentSign);
    BRDFData brdfData;
    half alpha = 1.0;
    InitializeBRDFData(
        surface.albedo,
        surface.metallic,
        half3(0.0, 0.0, 0.0),
        surface.smoothness,
        alpha,
        brdfData);
    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.Emission = 0.0;
    return UnityMetaFragment(metaInput);
}

#endif
