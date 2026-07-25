#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_FORWARD_PASS_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "TerrainSurfaceSampling.hlsl"

struct TS_Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TS_Varyings
{
    float4 controlUVAndLightmapUV : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 geometricNormalWS : TEXCOORD2;
    half3 vertexSH : TEXCOORD3;
    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight : TEXCOORD4;
    #else
        half fogFactor : TEXCOORD4;
    #endif
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord : TEXCOORD5;
    #endif
    #ifdef DYNAMICLIGHTMAP_ON
        float2 dynamicLightmapUV : TEXCOORD6;
    #endif
    #ifdef USE_APV_PROBE_OCCLUSION
        float4 probeOcclusion : TEXCOORD7;
    #endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

TS_Varyings TS_ForwardVertex(TS_Attributes input)
{
    TS_Varyings output = (TS_Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    TS_TerrainInstancing(input.positionOS, input.normalOS, input.texcoord);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.controlUVAndLightmapUV.xy = input.texcoord;
    output.controlUVAndLightmapUV.zw = input.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;
    output.positionWS = positionInputs.positionWS;
    output.geometricNormalWS = TransformObjectToWorldNormal(input.normalOS);
    OUTPUT_SH4(
        output.positionWS,
        output.geometricNormalWS,
        GetWorldSpaceNormalizeViewDir(output.positionWS),
        output.vertexSH,
        output.probeOcclusion);

    #ifdef DYNAMICLIGHTMAP_ON
        output.dynamicLightmapUV = input.texcoord * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    #endif

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

void TS_InitializeInputData(TS_Varyings input, float3 normalWS, out InputData inputData)
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
            input.controlUVAndLightmapUV.zw,
            input.dynamicLightmapUV,
            input.vertexSH,
            inputData.normalWS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(input.controlUVAndLightmapUV.zw);
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
        inputData.bakedGI = SAMPLE_GI(
            input.controlUVAndLightmapUV.zw,
            input.vertexSH,
            inputData.normalWS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(input.controlUVAndLightmapUV.zw);
    #endif
}

void TS_ForwardFragment(
    TS_Varyings input,
    out half4 outColor : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TS_ClipTerrainHoles(input.controlUVAndLightmapUV.xy);
    float3 geometricNormalWS = TS_GetGeometricNormalWS(
        input.controlUVAndLightmapUV.xy,
        input.geometricNormalWS);
    TS_Surface surface = TS_BuildSurface(
        input.controlUVAndLightmapUV.xy,
        input.positionWS,
        geometricNormalWS);

    InputData inputData;
    TS_InitializeInputData(input, surface.normalWS, inputData);
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

#endif
