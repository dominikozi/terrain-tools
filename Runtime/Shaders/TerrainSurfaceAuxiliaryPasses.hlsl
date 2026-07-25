#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_AUXILIARY_PASSES_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_AUXILIARY_PASSES_INCLUDED

#include "TerrainSurfaceSampling.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

struct TS_LeanAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TS_LeanVaryings
{
    float4 positionCS : SV_POSITION;
    float2 terrainUV : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 _LightDirection;
float3 _LightPosition;

TS_LeanVaryings TS_ShadowVertex(TS_LeanAttributes input)
{
    TS_LeanVaryings output = (TS_LeanVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    TS_TerrainInstancing(input.positionOS, input.normalOS, input.texcoord);
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
    output.terrainUV = input.texcoord;
    return output;
}

half4 TS_ShadowFragment(TS_LeanVaryings input) : SV_Target
{
    TS_ClipTerrainHoles(input.terrainUV);
    return 0.0;
}

TS_LeanVaryings TS_DepthVertex(TS_LeanAttributes input)
{
    TS_LeanVaryings output = (TS_LeanVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    TS_TerrainInstancing(input.positionOS, input.normalOS, input.texcoord);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.terrainUV = input.texcoord;
    return output;
}

half4 TS_DepthFragment(TS_LeanVaryings input) : SV_Target
{
    TS_ClipTerrainHoles(input.terrainUV);
    #ifdef SCENESELECTIONPASS
        return half4(_ObjectId, _PassValue, 1.0, 1.0);
    #else
        return input.positionCS.z;
    #endif
}

struct TS_DepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 terrainUV : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 geometricNormalWS : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

TS_DepthNormalsVaryings TS_DepthNormalsVertex(TS_LeanAttributes input)
{
    TS_DepthNormalsVaryings output = (TS_DepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    TS_TerrainInstancing(input.positionOS, input.normalOS, input.texcoord);
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.terrainUV = input.texcoord;
    output.geometricNormalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

void TS_DepthNormalsFragment(
    TS_DepthNormalsVaryings input,
    out half4 outNormalWS : SV_Target0
    #ifdef _WRITE_RENDERING_LAYERS
        , out uint outRenderingLayers : SV_Target1
    #endif
)
{
    TS_ClipTerrainHoles(input.terrainUV);
    float3 geometricNormalWS = TS_GetGeometricNormalWS(input.terrainUV, input.geometricNormalWS);
    TS_Surface surface = TS_BuildSurface(input.terrainUV, input.positionWS, geometricNormalWS);
    outNormalWS = half4(NormalizeNormalPerPixel(surface.normalWS), 0.0);
    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

struct TS_MetaAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TS_MetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 terrainUV : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 geometricNormalWS : TEXCOORD2;
};

TS_MetaVaryings TS_MetaVertex(TS_MetaAttributes input)
{
    TS_MetaVaryings output = (TS_MetaVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    TS_TerrainInstancing(input.positionOS, input.normalOS, input.uv0);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.geometricNormalWS = TransformObjectToWorldNormal(input.normalOS);
    output.terrainUV = input.uv0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv0, input.uv0);
    return output;
}

half4 TS_MetaFragment(TS_MetaVaryings input) : SV_Target
{
    TS_ClipTerrainHoles(input.terrainUV);
    float3 geometricNormalWS = NormalizeNormalPerPixel(input.geometricNormalWS);
    TS_Surface surface = TS_BuildSurface(input.terrainUV, input.positionWS, geometricNormalWS);
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
