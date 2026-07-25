#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_MESH_BLEND_SAMPLING_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_MESH_BLEND_SAMPLING_INCLUDED

#include "TerrainSurfaceSampling.hlsl"

struct TS_MeshSurface
{
    float3 albedo;
    float3 normalWS;
    float metallic;
    float smoothness;
    float occlusion;
    float terrainBlend;
};

int TS_FindBlendTile(float2 worldXZ, out float2 terrainUV)
{
    terrainUV = 0.0;
    int tileCount = clamp((int)_TS_BlendTileCount, 0, 4);
    [unroll]
    for (int tile = 0; tile < 4; tile++)
    {
        if (tile >= tileCount)
        {
            break;
        }

        float4 originSize = _TS_BlendTileOriginSize[tile];
        float2 uv = (worldXZ - originSize.xy) / max(originSize.zw, 1e-4);
        if (all(uv >= 0.0) && all(uv <= 1.0))
        {
            terrainUV = uv;
            return tile;
        }
    }
    return -1;
}

float4 TS_LoadBlendHeightTexture(int tileIndex, int2 sampleCoords)
{
    if (tileIndex == 0) return LOAD_TEXTURE2D(_TS_BlendHeight0, sampleCoords);
    if (tileIndex == 1) return LOAD_TEXTURE2D(_TS_BlendHeight1, sampleCoords);
    if (tileIndex == 2) return LOAD_TEXTURE2D(_TS_BlendHeight2, sampleCoords);
    return LOAD_TEXTURE2D(_TS_BlendHeight3, sampleCoords);
}

int2 TS_GetBlendHeightSampleCoords(int tileIndex, float2 terrainUV)
{
    float2 resolution = max(_TS_BlendTileHeightData[tileIndex].zw, 1.0);
    float2 maximumCoords = max(resolution - 1.0, 0.0);
    return int2(round(saturate(terrainUV) * maximumCoords));
}

float TS_LoadBlendTerrainHeight(int tileIndex, int2 sampleCoords)
{
    float rawHeight = UnpackHeightmap(TS_LoadBlendHeightTexture(tileIndex, sampleCoords));
    float4 heightData = _TS_BlendTileHeightData[tileIndex];
    return heightData.x + rawHeight * heightData.y;
}

float TS_SampleBlendTerrainHeight(int tileIndex, float2 uv)
{
    return TS_LoadBlendTerrainHeight(tileIndex, TS_GetBlendHeightSampleCoords(tileIndex, uv));
}

float3 TS_SampleBlendTerrainNormal(int tileIndex, float2 uv)
{
    float4 originSize = _TS_BlendTileOriginSize[tileIndex];
    float4 heightData = _TS_BlendTileHeightData[tileIndex];
    int2 resolution = max((int2)round(heightData.zw), int2(1, 1));
    int2 maximumCoords = max(resolution - 1, int2(0, 0));
    int2 center = TS_GetBlendHeightSampleCoords(tileIndex, uv);
    int2 left = int2(max(center.x - 1, 0), center.y);
    int2 right = int2(min(center.x + 1, maximumCoords.x), center.y);
    int2 down = int2(center.x, max(center.y - 1, 0));
    int2 up = int2(center.x, min(center.y + 1, maximumCoords.y));
    float heightLeft = TS_LoadBlendTerrainHeight(tileIndex, left);
    float heightRight = TS_LoadBlendTerrainHeight(tileIndex, right);
    float heightDown = TS_LoadBlendTerrainHeight(tileIndex, down);
    float heightUp = TS_LoadBlendTerrainHeight(tileIndex, up);
    float worldStepX = max(
        (right.x - left.x) * originSize.z / max((float)maximumCoords.x, 1.0),
        1e-4);
    float worldStepZ = max(
        (up.y - down.y) * originSize.w / max((float)maximumCoords.y, 1.0),
        1e-4);
    float derivativeX = (heightRight - heightLeft) / worldStepX;
    float derivativeZ = (heightUp - heightDown) / worldStepZ;
    return normalize(float3(-derivativeX, 1.0, -derivativeZ));
}

void TS_SampleBlendControls(
    int tileIndex,
    float2 terrainUV,
    out float4 control0,
    out float4 control1,
    out float4 control2,
    out float4 control3,
    out float4 control4)
{
    float4 texelSize = _TS_BlendTileControlTexelSize[tileIndex];
    float2 uv = (terrainUV * (texelSize.zw - 1.0) + 0.5) * texelSize.xy;
    control3 = 0.0;
    control4 = 0.0;
    if (tileIndex == 0)
    {
        control0 = SAMPLE_TEXTURE2D(_TS_BlendControl00, sampler_TS_BlendControl00, uv);
        control1 = SAMPLE_TEXTURE2D(_TS_BlendControl01, sampler_TS_BlendControl01, uv);
        control2 = SAMPLE_TEXTURE2D(_TS_BlendControl02, sampler_TS_BlendControl02, uv);
        if (_TS_ActiveLayerCount > 12.0)
            control3 = SAMPLE_TEXTURE2D(_TS_BlendControl03, sampler_TS_BlendControl03, uv);
        if (_TS_ActiveLayerCount > 16.0)
            control4 = SAMPLE_TEXTURE2D(_TS_BlendControl04, sampler_TS_BlendControl04, uv);
    }
    else if (tileIndex == 1)
    {
        control0 = SAMPLE_TEXTURE2D(_TS_BlendControl10, sampler_TS_BlendControl10, uv);
        control1 = SAMPLE_TEXTURE2D(_TS_BlendControl11, sampler_TS_BlendControl11, uv);
        control2 = SAMPLE_TEXTURE2D(_TS_BlendControl12, sampler_TS_BlendControl12, uv);
        if (_TS_ActiveLayerCount > 12.0)
            control3 = SAMPLE_TEXTURE2D(_TS_BlendControl13, sampler_TS_BlendControl13, uv);
        if (_TS_ActiveLayerCount > 16.0)
            control4 = SAMPLE_TEXTURE2D(_TS_BlendControl14, sampler_TS_BlendControl14, uv);
    }
    else if (tileIndex == 2)
    {
        control0 = SAMPLE_TEXTURE2D(_TS_BlendControl20, sampler_TS_BlendControl20, uv);
        control1 = SAMPLE_TEXTURE2D(_TS_BlendControl21, sampler_TS_BlendControl21, uv);
        control2 = SAMPLE_TEXTURE2D(_TS_BlendControl22, sampler_TS_BlendControl22, uv);
        if (_TS_ActiveLayerCount > 12.0)
            control3 = SAMPLE_TEXTURE2D(_TS_BlendControl23, sampler_TS_BlendControl23, uv);
        if (_TS_ActiveLayerCount > 16.0)
            control4 = SAMPLE_TEXTURE2D(_TS_BlendControl24, sampler_TS_BlendControl24, uv);
    }
    else
    {
        control0 = SAMPLE_TEXTURE2D(_TS_BlendControl30, sampler_TS_BlendControl30, uv);
        control1 = SAMPLE_TEXTURE2D(_TS_BlendControl31, sampler_TS_BlendControl31, uv);
        control2 = SAMPLE_TEXTURE2D(_TS_BlendControl32, sampler_TS_BlendControl32, uv);
        if (_TS_ActiveLayerCount > 12.0)
            control3 = SAMPLE_TEXTURE2D(_TS_BlendControl33, sampler_TS_BlendControl33, uv);
        if (_TS_ActiveLayerCount > 16.0)
            control4 = SAMPLE_TEXTURE2D(_TS_BlendControl34, sampler_TS_BlendControl34, uv);
    }
}

TS_MeshSurface TS_BuildMeshSurface(
    float2 baseUV,
    float4 vertexColor,
    float3 positionWS,
    float3 geometricNormalWS,
    float3 tangentWS,
    float tangentSign)
{
    float4 baseAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColor;
    baseAlbedo.rgb *= vertexColor.rgb;
    float3 baseNormalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_BaseNormal, sampler_BaseNormal, baseUV),
        _BaseNormalScale);
    float3 bitangentWS = tangentSign * cross(geometricNormalWS, tangentWS);
    float3 baseNormalWS = NormalizeNormalPerPixel(
        TransformTangentToWorld(baseNormalTS, float3x3(tangentWS, bitangentWS, geometricNormalWS)));
    float4 baseMask = SAMPLE_TEXTURE2D(_BaseMask, sampler_BaseMask, baseUV);
    float baseMetallic = lerp(_BaseMetallic, baseMask.r, _BaseHasMask);
    float baseOcclusion = lerp(1.0, baseMask.g, _BaseHasMask);
    float baseSmoothness = lerp(_BaseSmoothness, baseMask.a, _BaseHasMask);

    TS_MeshSurface result;
    result.albedo = baseAlbedo.rgb;
    result.normalWS = baseNormalWS;
    result.metallic = baseMetallic;
    result.smoothness = baseSmoothness;
    result.occlusion = baseOcclusion;
    result.terrainBlend = 0.0;

    float2 terrainUV;
    int foundTileIndex = TS_FindBlendTile(positionWS.xz, terrainUV);
    int tileIndex = max(foundTileIndex, 0);
    float terrainHeight = TS_SampleBlendTerrainHeight(tileIndex, terrainUV) + _TS_MeshBlendParameters.y;
    float blendThreshold = _TS_MeshBlendParameters.x;
    if (_TS_MeshBlendParameters.w > 0.0001)
    {
        float noise = SAMPLE_TEXTURE2D(
            _TS_MeshBlendNoise,
            sampler_TS_MeshBlendNoise,
            positionWS.xz * _TS_MeshBlendParameters.z).r * 2.0 - 1.0;
        blendThreshold *= max(0.05, 1.0 + noise * _TS_MeshBlendParameters.w);
    }

    float signedHeightDelta = positionWS.y - terrainHeight;
    float normalizedContactDistance = saturate(max(signedHeightDelta, 0.0) / max(blendThreshold, 1e-4));
    float blend = 1.0 - smoothstep(0.0, 1.0, normalizedContactDistance);
    blend *= 1.0 - step(blendThreshold, -signedHeightDelta);
    blend = pow(saturate(blend), _TS_MeshBlendExtra.y);
    blend *= lerp(1.0, vertexColor.a, _TS_MeshBlendExtra.x);
    blend *= foundTileIndex >= 0 ? 1.0 : 0.0;

    float4 control0;
    float4 control1;
    float4 control2;
    float4 control3;
    float4 control4;
    TS_SampleBlendControls(tileIndex, terrainUV, control0, control1, control2, control3, control4);
    TS_TopLayers top = TS_SelectTopLayersFromControlValues(control0, control1, control2, control3, control4);
    float3 terrainGeometricNormalWS = TS_SampleBlendTerrainNormal(tileIndex, terrainUV);
    float2 terrainLocalPosition = positionWS.xz - _TS_BlendTileOriginSize[tileIndex].xy;
    TS_Surface terrainSurface = TS_BuildSurfaceFromTop(
        top,
        positionWS,
        terrainLocalPosition,
        terrainGeometricNormalWS);

    result.albedo = lerp(result.albedo, terrainSurface.albedo, blend);
    result.normalWS = normalize(lerp(
        result.normalWS,
        terrainSurface.normalWS,
        blend * _TS_MeshBlendExtra.z));
    result.metallic = lerp(result.metallic, terrainSurface.metallic, blend);
    result.smoothness = lerp(result.smoothness, terrainSurface.smoothness, blend);
    result.occlusion = lerp(result.occlusion, terrainSurface.occlusion, blend);
    result.terrainBlend = blend;
    return result;
}

#endif
