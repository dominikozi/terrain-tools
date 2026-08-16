#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_INPUT_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#define TS_MAX_LAYERS 20

CBUFFER_START(UnityPerMaterial)
    float _TS_ActiveLayerCount;
    float _TS_BlendQuality;
    float _TS_HeightBlendEnabled;
    float4 _TS_HeightParameters;
    float4 _TS_TerrainOriginSize;
    float4 _TS_TerrainSizeY;
    float4 _TS_ControlTexelSize;

    float4 _TS_LayerTiling[TS_MAX_LAYERS];
    float4 _TS_LayerTint[TS_MAX_LAYERS];
    float4 _TS_LayerHeightSurface[TS_MAX_LAYERS];
    float4 _TS_LayerSurfaceExtra[TS_MAX_LAYERS];
    float4 _TS_LayerAntiTiling[TS_MAX_LAYERS];
    float4 _TS_LayerTriplanar[TS_MAX_LAYERS];

    float4 _TS_AntiTilingFlags;
    float4 _TS_DetailNoiseParameters;
    float4 _TS_DetailNoiseFade;
    float4 _TS_MacroNoiseParameters;
    float4 _TS_MacroNoiseFade;
    float4 _TS_NormalNoiseParameters;
    float4 _TS_NormalNoiseFade;
    float4 _TS_DistanceResampleParameters;
    float4 _TS_DistanceResampleFade;

    float4 _TS_StochasticParameters;
    float4 _TS_StochasticExtra;

    float4 _TS_GlobalFlags;
    float4 _TS_GlobalMapping;
    float4 _TS_GlobalParameters;
    float4 _TS_GlobalFade;
    float4 _TS_GlobalFadeOpacity;
    float4 _TS_GlobalReplacement;

    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _BaseNormalScale;
    float _BaseMetallic;
    float _BaseSmoothness;
    float _BaseHasMask;
    float4 _TS_MeshBlendParameters;
    float4 _TS_MeshBlendExtra;
    float _TS_BlendTileCount;
    float4 _TS_BlendTileOriginSize[4];
    float4 _TS_BlendTileHeightData[4];
    float4 _TS_BlendTileControlTexelSize[4];
CBUFFER_END

CBUFFER_START(_Terrain)
    float4 _TerrainHeightmapRecipSize;
    float4 _TerrainHeightmapScale;
    #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
    #endif
CBUFFER_END

TEXTURE2D_ARRAY(_TS_AlbedoHeightArray);
TEXTURE2D_ARRAY(_TS_NormalSurfaceArray);
TEXTURE2D_ARRAY(_TS_MetallicArray);
SAMPLER(sampler_TS_AlbedoHeightArray);
SAMPLER(sampler_TS_NormalSurfaceArray);
SAMPLER(sampler_TS_MetallicArray);

TEXTURE2D(_TS_Control0);
TEXTURE2D(_TS_Control1);
TEXTURE2D(_TS_Control2);
TEXTURE2D(_TS_Control3);
TEXTURE2D(_TS_Control4);

TEXTURE2D(_TS_DetailNoise);
TEXTURE2D(_TS_MacroNoise);
TEXTURE2D(_TS_NormalNoise);
TEXTURE2D(_TS_GlobalTint);
TEXTURE2D(_TS_GlobalNormal);
TEXTURE2D(_BaseMap);
TEXTURE2D(_BaseNormal);
TEXTURE2D(_BaseMask);
TEXTURE2D(_TS_MeshBlendNoise);

TEXTURE2D(_TS_BlendHeight0);
TEXTURE2D(_TS_BlendHeight1);
TEXTURE2D(_TS_BlendHeight2);
TEXTURE2D(_TS_BlendHeight3);

TEXTURE2D(_TS_BlendControl00);
TEXTURE2D(_TS_BlendControl01);
TEXTURE2D(_TS_BlendControl02);
TEXTURE2D(_TS_BlendControl03);
TEXTURE2D(_TS_BlendControl04);
TEXTURE2D(_TS_BlendControl10);
TEXTURE2D(_TS_BlendControl11);
TEXTURE2D(_TS_BlendControl12);
TEXTURE2D(_TS_BlendControl13);
TEXTURE2D(_TS_BlendControl14);
TEXTURE2D(_TS_BlendControl20);
TEXTURE2D(_TS_BlendControl21);
TEXTURE2D(_TS_BlendControl22);
TEXTURE2D(_TS_BlendControl23);
TEXTURE2D(_TS_BlendControl24);
TEXTURE2D(_TS_BlendControl30);
TEXTURE2D(_TS_BlendControl31);
TEXTURE2D(_TS_BlendControl32);
TEXTURE2D(_TS_BlendControl33);
TEXTURE2D(_TS_BlendControl34);

TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
TEXTURE2D(_TerrainHolesTexture);

#define sampler_TS_Control0 sampler_LinearClamp
#define sampler_TS_Control1 sampler_LinearClamp
#define sampler_TS_Control2 sampler_LinearClamp
#define sampler_TS_Control3 sampler_LinearClamp
#define sampler_TS_Control4 sampler_LinearClamp
#define sampler_TS_DetailNoise sampler_LinearRepeat
#define sampler_TS_MacroNoise sampler_LinearRepeat
#define sampler_TS_NormalNoise sampler_LinearRepeat
#define sampler_TS_GlobalTint sampler_LinearRepeat
#define sampler_TS_GlobalNormal sampler_LinearRepeat
#define sampler_BaseMap sampler_LinearRepeat
#define sampler_BaseNormal sampler_LinearRepeat
#define sampler_BaseMask sampler_LinearRepeat
#define sampler_TS_MeshBlendNoise sampler_LinearRepeat
#define sampler_TS_BlendHeight0 sampler_LinearClamp
#define sampler_TS_BlendHeight1 sampler_LinearClamp
#define sampler_TS_BlendHeight2 sampler_LinearClamp
#define sampler_TS_BlendHeight3 sampler_LinearClamp
#define sampler_TS_BlendControl00 sampler_LinearClamp
#define sampler_TS_BlendControl01 sampler_LinearClamp
#define sampler_TS_BlendControl02 sampler_LinearClamp
#define sampler_TS_BlendControl03 sampler_LinearClamp
#define sampler_TS_BlendControl04 sampler_LinearClamp
#define sampler_TS_BlendControl10 sampler_LinearClamp
#define sampler_TS_BlendControl11 sampler_LinearClamp
#define sampler_TS_BlendControl12 sampler_LinearClamp
#define sampler_TS_BlendControl13 sampler_LinearClamp
#define sampler_TS_BlendControl14 sampler_LinearClamp
#define sampler_TS_BlendControl20 sampler_LinearClamp
#define sampler_TS_BlendControl21 sampler_LinearClamp
#define sampler_TS_BlendControl22 sampler_LinearClamp
#define sampler_TS_BlendControl23 sampler_LinearClamp
#define sampler_TS_BlendControl24 sampler_LinearClamp
#define sampler_TS_BlendControl30 sampler_LinearClamp
#define sampler_TS_BlendControl31 sampler_LinearClamp
#define sampler_TS_BlendControl32 sampler_LinearClamp
#define sampler_TS_BlendControl33 sampler_LinearClamp
#define sampler_TS_BlendControl34 sampler_LinearClamp
#define sampler_TerrainNormalmapTexture sampler_LinearClamp
#define sampler_TerrainHolesTexture sampler_LinearClamp

UNITY_INSTANCING_BUFFER_START(Terrain)
    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
UNITY_INSTANCING_BUFFER_END(Terrain)

void TS_TerrainInstancing(inout float4 positionOS, inout float3 normalOS, inout float2 uv)
{
    #ifdef UNITY_INSTANCING_ENABLED
        float2 patchVertex = positionOS.xy;
        float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
        float2 sampleCoords = (patchVertex + instanceData.xy) * instanceData.z;
        float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

        positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
        positionOS.y = height * _TerrainHeightmapScale.y;
        normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
        uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
    #endif
}

void TS_TerrainInstancing(inout float4 positionOS, inout float3 normalOS)
{
    float2 uv = 0.0;
    TS_TerrainInstancing(positionOS, normalOS, uv);
}

void TS_ClipTerrainHoles(float2 uv)
{
    #ifdef _ALPHATEST_ON
        float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
        clip(hole < 0.0005 ? -1.0 : 1.0);
    #endif
}

float2 TS_ControlUV(float2 terrainUV)
{
    return (terrainUV * (_TS_ControlTexelSize.zw - 1.0) + 0.5) * _TS_ControlTexelSize.xy;
}

float3 TS_GetGeometricNormalWS(float2 terrainUV, float3 interpolatedNormalWS)
{
    #ifdef UNITY_INSTANCING_ENABLED
        float2 sampleCoords =
            (terrainUV / _TerrainHeightmapRecipSize.zw + 0.5) * _TerrainHeightmapRecipSize.xy;
        float3 normalOS = normalize(
            SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2.0 - 1.0);
        return NormalizeNormalPerPixel(TransformObjectToWorldNormal(normalOS));
    #else
        return NormalizeNormalPerPixel(interpolatedNormalWS);
    #endif
}

#endif
