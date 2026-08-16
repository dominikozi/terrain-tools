#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_SAMPLING_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_SAMPLING_INCLUDED

#include "TerrainSurfaceInput.hlsl"

struct TS_TopLayers
{
    int4 indices;
    float4 weights;
};

struct TS_LayerSurface
{
    float3 albedo;
    float height;
    float3 normalWS;
    float metallic;
    float smoothness;
    float occlusion;
};

struct TS_Surface
{
    float3 albedo;
    float3 normalWS;
    float metallic;
    float smoothness;
    float occlusion;
};

void TS_ApplyLayerTint(int layerIndex, inout TS_LayerSurface surface)
{
    float4 tint = _TS_LayerTint[layerIndex];
    [branch]
    if (tint.a > 0.5)
    {
        surface.albedo *= tint.rgb;
    }
}

float TS_SmoothRange(float value, float2 range)
{
    return smoothstep(range.x, max(range.y, range.x + 1e-4), value);
}

void TS_InsertCandidate(int layerIndex, float candidateWeight, inout TS_TopLayers top)
{
    if (candidateWeight <= top.weights.w)
    {
        return;
    }

    if (candidateWeight > top.weights.x)
    {
        top.weights.w = top.weights.z;
        top.indices.w = top.indices.z;
        top.weights.z = top.weights.y;
        top.indices.z = top.indices.y;
        top.weights.y = top.weights.x;
        top.indices.y = top.indices.x;
        top.weights.x = candidateWeight;
        top.indices.x = layerIndex;
    }
    else if (candidateWeight > top.weights.y)
    {
        top.weights.w = top.weights.z;
        top.indices.w = top.indices.z;
        top.weights.z = top.weights.y;
        top.indices.z = top.indices.y;
        top.weights.y = candidateWeight;
        top.indices.y = layerIndex;
    }
    else if (candidateWeight > top.weights.z)
    {
        top.weights.w = top.weights.z;
        top.indices.w = top.indices.z;
        top.weights.z = candidateWeight;
        top.indices.z = layerIndex;
    }
    else
    {
        top.weights.w = candidateWeight;
        top.indices.w = layerIndex;
    }
}

void TS_InsertControl(float4 control, int firstLayer, int activeLayerCount, inout TS_TopLayers top)
{
    if (firstLayer < activeLayerCount) TS_InsertCandidate(firstLayer, control.r, top);
    if (firstLayer + 1 < activeLayerCount) TS_InsertCandidate(firstLayer + 1, control.g, top);
    if (firstLayer + 2 < activeLayerCount) TS_InsertCandidate(firstLayer + 2, control.b, top);
    if (firstLayer + 3 < activeLayerCount) TS_InsertCandidate(firstLayer + 3, control.a, top);
}

TS_TopLayers TS_SelectTopLayersFromControlValues(
    float4 control0,
    float4 control1,
    float4 control2,
    float4 control3,
    float4 control4)
{
    TS_TopLayers top;
    top.indices = 0;
    top.weights = -1.0;

    int activeLayerCount = clamp((int)_TS_ActiveLayerCount, 1, TS_MAX_LAYERS);
    TS_InsertControl(control0, 0, activeLayerCount, top);
    TS_InsertControl(control1, 4, activeLayerCount, top);
    TS_InsertControl(control2, 8, activeLayerCount, top);

    [branch]
    if (activeLayerCount > 12)
    {
        TS_InsertControl(control3, 12, activeLayerCount, top);
    }

    [branch]
    if (activeLayerCount > 16)
    {
        TS_InsertControl(control4, 16, activeLayerCount, top);
    }

    top.weights = max(top.weights, 0.0);
    int quality = clamp((int)_TS_BlendQuality, 2, 4);
    if (quality < 4) top.weights.w = 0.0;
    if (quality < 3) top.weights.z = 0.0;
    float sum = dot(top.weights, 1.0);
    if (sum < 1e-6)
    {
        top.indices = 0;
        top.weights = float4(1.0, 0.0, 0.0, 0.0);
    }
    else
    {
        top.weights /= sum;
    }

    return top;
}

TS_TopLayers TS_SelectTopLayers(float2 terrainUV)
{
    int activeLayerCount = clamp((int)_TS_ActiveLayerCount, 1, TS_MAX_LAYERS);
    float2 controlUV = TS_ControlUV(terrainUV);
    float4 control3 = 0.0;
    float4 control4 = 0.0;
    [branch]
    if (activeLayerCount > 12)
    {
        control3 = SAMPLE_TEXTURE2D(_TS_Control3, sampler_TS_Control3, controlUV);
    }
    [branch]
    if (activeLayerCount > 16)
    {
        control4 = SAMPLE_TEXTURE2D(_TS_Control4, sampler_TS_Control4, controlUV);
    }
    return TS_SelectTopLayersFromControlValues(
        SAMPLE_TEXTURE2D(_TS_Control0, sampler_TS_Control0, controlUV),
        SAMPLE_TEXTURE2D(_TS_Control1, sampler_TS_Control1, controlUV),
        SAMPLE_TEXTURE2D(_TS_Control2, sampler_TS_Control2, controlUV),
        control3,
        control4);
}

float3 TS_DecodeNormal(float2 encodedNormal, float strength)
{
    float2 xy = encodedNormal * 2.0 - 1.0;
    float z = sqrt(saturate(1.0 - dot(xy, xy)));
    return float3(xy * strength, z);
}

float3 TS_TransformProjectionNormal(float3 normalTS, int axis, float axisSign)
{
    float3 tangentWS;
    float3 bitangentWS;
    float3 projectionNormalWS;
    if (axis == 0)
    {
        tangentWS = float3(0.0, 0.0, -axisSign);
        bitangentWS = float3(0.0, 1.0, 0.0);
        projectionNormalWS = float3(axisSign, 0.0, 0.0);
    }
    else if (axis == 1)
    {
        tangentWS = float3(-axisSign, 0.0, 0.0);
        bitangentWS = float3(0.0, 0.0, 1.0);
        projectionNormalWS = float3(0.0, axisSign, 0.0);
    }
    else
    {
        tangentWS = float3(axisSign, 0.0, 0.0);
        bitangentWS = float3(0.0, 1.0, 0.0);
        projectionNormalWS = float3(0.0, 0.0, axisSign);
    }

    return normalize(
        normalTS.x * tangentWS + normalTS.y * bitangentWS + normalTS.z * projectionNormalWS);
}

float3 TS_TransformTerrainNormal(float3 normalTS, float3 geometricNormalWS)
{
    float3 terrainTangentWS = cross(float3(0.0, 0.0, 1.0), geometricNormalWS);
    if (dot(terrainTangentWS, terrainTangentWS) < 1e-4)
    {
        terrainTangentWS = float3(-1.0, 0.0, 0.0);
    }

    // Match URP TerrainLit exactly: the matrix rows are
    // -cross(Z, N), cross(N, cross(Z, N)), N. Do not normalize the first
    // two rows independently because TerrainLit does not do that either.
    float3 tangentWS = -terrainTangentWS;
    float3 bitangentWS = cross(geometricNormalWS, terrainTangentWS);
    return normalize(
        normalTS.x * tangentWS + normalTS.y * bitangentWS + normalTS.z * geometricNormalWS);
}

float4 TS_SampleAlbedoHeight(int layerIndex, float2 uv, float2 uvDx, float2 uvDy)
{
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(
        _TS_AlbedoHeightArray,
        sampler_TS_AlbedoHeightArray,
        uv,
        layerIndex,
        uvDx,
        uvDy);
}

float4 TS_SampleNormalSurface(int layerIndex, float2 uv, float2 uvDx, float2 uvDy)
{
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(
        _TS_NormalSurfaceArray,
        sampler_TS_NormalSurfaceArray,
        uv,
        layerIndex,
        uvDx,
        uvDy);
}

float TS_SampleMetallic(int layerIndex, float2 uv, float2 uvDx, float2 uvDy)
{
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(
        _TS_MetallicArray,
        sampler_TS_MetallicArray,
        uv,
        layerIndex,
        uvDx,
        uvDy).r;
}

#include "TerrainSurfaceStochastic.hlsl"

TS_LayerSurface TS_SampleProjection(
    int layerIndex,
    float2 projectionPosition,
    float2 projectionDx,
    float2 projectionDy,
    float2 stochasticPosition,
    float3 geometricNormalWS,
    int projectionAxis,
    float projectionSign,
    float cameraDistance)
{
    float4 tiling = _TS_LayerTiling[layerIndex];
    float triplanarScale = projectionAxis >= 0 ? _TS_LayerTriplanar[layerIndex].y : 1.0;
    float2 uv = projectionPosition * tiling.xy * triplanarScale + tiling.zw;
    float2 uvDx = projectionDx * tiling.xy * triplanarScale;
    float2 uvDy = projectionDy * tiling.xy * triplanarScale;
    float2 stochasticGridUV =
        stochasticPosition * tiling.xy * triplanarScale + tiling.zw;
    TS_TextureSetSample textureSet = TS_SampleTextureSet(
        layerIndex, uv, uvDx, uvDy, stochasticGridUV);
    float4 albedoHeight = textureSet.albedoHeight;
    float4 normalSurface = textureSet.normalSurface;
    float metallic = textureSet.metallic;

    float distanceResampleAmount =
        _TS_AntiTilingFlags.w *
        _TS_LayerAntiTiling[layerIndex].w *
        _TS_DistanceResampleParameters.y *
        TS_SmoothRange(cameraDistance, _TS_DistanceResampleFade.xy);
    if (distanceResampleAmount > 1e-4)
    {
        float resampleScale = _TS_DistanceResampleParameters.x;
        TS_TextureSetSample distantTextureSet = TS_SampleTextureSet(
            layerIndex,
            uv * resampleScale,
            uvDx * resampleScale,
            uvDy * resampleScale,
            stochasticGridUV * resampleScale);
        float4 distantAlbedoHeight = distantTextureSet.albedoHeight;
        float4 distantNormalSurface = distantTextureSet.normalSurface;
        float distantMetallic = distantTextureSet.metallic;
        float blend = distanceResampleAmount;
        if (_TS_DistanceResampleParameters.z > 0.5)
        {
            float heightDifference = distantAlbedoHeight.a - albedoHeight.a;
            blend = saturate(blend + heightDifference / max(_TS_HeightParameters.x, 1e-4));
        }

        albedoHeight = lerp(albedoHeight, distantAlbedoHeight, blend);
        normalSurface = lerp(normalSurface, distantNormalSurface, blend);
        metallic = lerp(metallic, distantMetallic, blend);
    }

    [branch]
    if (_TS_AntiTilingFlags.x > 0.5)
    {
        float detailFade = 1.0 - TS_SmoothRange(cameraDistance, _TS_DetailNoiseFade.xy);
        float detailNoise = SAMPLE_TEXTURE2D(
            _TS_DetailNoise,
            sampler_TS_DetailNoise,
            projectionPosition * _TS_DetailNoiseParameters.x).r * 2.0 - 1.0;
        float detailStrength =
            _TS_DetailNoiseParameters.y * _TS_LayerAntiTiling[layerIndex].x * detailFade;
        albedoHeight.rgb *= max(0.0, 1.0 + detailNoise * detailStrength);
    }

    [branch]
    if (_TS_AntiTilingFlags.y > 0.5)
    {
        float macroFade = TS_SmoothRange(cameraDistance, _TS_MacroNoiseFade.xy);
        float macroNoise = SAMPLE_TEXTURE2D(
            _TS_MacroNoise,
            sampler_TS_MacroNoise,
            projectionPosition * _TS_MacroNoiseParameters.x).r * 2.0 - 1.0;
        float macroStrength =
            _TS_MacroNoiseParameters.y * _TS_LayerAntiTiling[layerIndex].y * macroFade;
        albedoHeight.rgb *= max(0.0, 1.0 + macroNoise * macroStrength);
    }

    float normalStrength = _TS_LayerHeightSurface[layerIndex].w;
    float3 normalTS = float3(0.0, 0.0, 1.0);
    [branch]
    if (normalStrength > 1e-4)
    {
        normalTS = TS_DecodeNormal(normalSurface.rg, normalStrength);
        [branch]
        if (_TS_AntiTilingFlags.z > 0.5)
        {
            float normalNoiseFade = 1.0 - TS_SmoothRange(cameraDistance, _TS_NormalNoiseFade.xy);
            float3 noiseNormalTS = UnpackNormalScale(
                SAMPLE_TEXTURE2D(
                    _TS_NormalNoise,
                    sampler_TS_NormalNoise,
                    projectionPosition * _TS_NormalNoiseParameters.x),
                _TS_NormalNoiseParameters.y * _TS_LayerAntiTiling[layerIndex].z * normalNoiseFade);
            normalTS = normalize(float3(normalTS.xy + noiseNormalTS.xy, normalTS.z));
        }
    }

    TS_LayerSurface surface;
    surface.albedo = albedoHeight.rgb;
    surface.height = albedoHeight.a;
    surface.normalWS = projectionAxis >= 0
        ? TS_TransformProjectionNormal(normalTS, projectionAxis, projectionSign)
        : TS_TransformTerrainNormal(normalTS, geometricNormalWS);
    surface.metallic = saturate(metallic * _TS_LayerHeightSurface[layerIndex].z);
    surface.smoothness = saturate(normalSurface.a * _TS_LayerSurfaceExtra[layerIndex].x);
    surface.occlusion = saturate(lerp(1.0, normalSurface.b, _TS_LayerSurfaceExtra[layerIndex].y));
    return surface;
}

float3 TS_GetDominantProjectionAxis(float3 projectionNormalWS)
{
    float3 absoluteNormal = abs(projectionNormalWS);

    // Prefer the top projection at an exact top/side tie. This keeps the
    // projection on the terrain floor stable at the foot of a sharp cliff.
    if (absoluteNormal.y >= absoluteNormal.x && absoluteNormal.y >= absoluteNormal.z)
    {
        return float3(0.0, 1.0, 0.0);
    }

    return absoluteNormal.x >= absoluteNormal.z
        ? float3(1.0, 0.0, 0.0)
        : float3(0.0, 0.0, 1.0);
}

float3 TS_GetRasterizedSurfaceNormal(
    float3 positionDx,
    float3 positionDy,
    float3 geometricNormalWS)
{
    float3 rasterizedNormalWS = cross(positionDy, positionDx);
    float normalLengthSquared = dot(rasterizedNormalWS, rasterizedNormalWS);
    if (normalLengthSquared < 1e-10)
    {
        return geometricNormalWS;
    }

    rasterizedNormalWS *= rsqrt(normalLengthSquared);
    return dot(rasterizedNormalWS, geometricNormalWS) < 0.0
        ? -rasterizedNormalWS
        : rasterizedNormalWS;
}

float TS_GetProjectionAxisLock(
    float3 rasterizedNormalWS,
    float3 geometricNormalWS)
{
    float normalMismatch = 1.0 - saturate(dot(rasterizedNormalWS, geometricNormalWS));

    // The terrain normal texture is filtered and can leak a cliff normal onto
    // the adjacent floor. Between roughly 10 and 20 degrees of disagreement,
    // progressively trust the rasterized surface and lock to one projection.
    return smoothstep(0.015, 0.060, normalMismatch);
}

TS_LayerSurface TS_SampleLayer(
    int layerIndex,
    float3 positionWS,
    float2 planarPosition,
    float3 geometricNormalWS,
    float cameraDistance)
{
    float3 positionDx = ddx(positionWS);
    float3 positionDy = ddy(positionWS);
    if (_TS_LayerTriplanar[layerIndex].x < 0.5)
    {
        return TS_SampleProjection(
            layerIndex,
            planarPosition,
            ddx(planarPosition),
            ddy(planarPosition),
            positionWS.xz,
            geometricNormalWS,
            -1,
            1.0,
            cameraDistance);
    }

    float3 rasterizedNormalWS = TS_GetRasterizedSurfaceNormal(
        positionDx,
        positionDy,
        geometricNormalWS);
    float projectionAxisLock = TS_GetProjectionAxisLock(
        rasterizedNormalWS,
        geometricNormalWS);
    float3 projectionNormalWS = normalize(lerp(
        geometricNormalWS,
        rasterizedNormalWS,
        projectionAxisLock));

    float3 signs = sign(projectionNormalWS);
    signs = signs + (1.0 - abs(signs));
    TS_LayerSurface axisX = TS_SampleProjection(
        layerIndex, positionWS.zy, positionDx.zy, positionDy.zy, positionWS.zy,
        geometricNormalWS, 0, signs.x, cameraDistance);
    TS_LayerSurface axisY = TS_SampleProjection(
        layerIndex, positionWS.xz, positionDx.xz, positionDy.xz, positionWS.xz,
        geometricNormalWS, 1, signs.y, cameraDistance);
    TS_LayerSurface axisZ = TS_SampleProjection(
        layerIndex, positionWS.xy, positionDx.xy, positionDy.xy, positionWS.xy,
        geometricNormalWS, 2, signs.z, cameraDistance);

    float sharpness = _TS_LayerTriplanar[layerIndex].z;
    float3 axisWeights = pow(max(abs(projectionNormalWS), 1e-4), sharpness);
    axisWeights /= max(dot(axisWeights, 1.0), 1e-5);

    float3 weightedHeight = float3(axisX.height, axisY.height, axisZ.height) * axisWeights;
    float maxHeight = max(weightedHeight.x, max(weightedHeight.y, weightedHeight.z));
    float transition = max(_TS_LayerTriplanar[layerIndex].w, 1e-4);
    axisWeights = max(0.0, weightedHeight + transition - maxHeight) * axisWeights + 1e-6;
    axisWeights /= max(dot(axisWeights, 1.0), 1e-5);

    float3 dominantAxisWeights = TS_GetDominantProjectionAxis(projectionNormalWS);
    axisWeights = lerp(axisWeights, dominantAxisWeights, projectionAxisLock);

    // A heightfield cliff contains a narrow row of transition triangles at its
    // foot. Never blend their side UVs into the top projection: choose one
    // family explicitly. Triplanar sharpness also biases the boundary toward
    // the top projection, so larger values reserve side projection for
    // progressively steeper, genuinely cliff-like geometry.
    float topProjectionScore =
        abs(rasterizedNormalWS.y) * max(sharpness, 1.0);
    float sideProjectionScore =
        max(abs(rasterizedNormalWS.x), abs(rasterizedNormalWS.z));
    float useTopProjection = step(sideProjectionScore, topProjectionScore);

    float3 sideAxisWeights = float3(axisWeights.x, 0.0, axisWeights.z);
    float sideAxisWeightSum = sideAxisWeights.x + sideAxisWeights.z;
    float hasStableSideProjection = step(1e-5, sideAxisWeightSum);

    // If height-aware projection weighting eliminated both side axes, treating
    // the fragment as a side would leave weights whose sum is below one and
    // produce black, block-shaped artifacts. Keep the top projection instead.
    useTopProjection = max(useTopProjection, 1.0 - hasStableSideProjection);
    sideAxisWeights /= max(sideAxisWeightSum, 1e-5);
    float3 fallbackSideAxis = abs(rasterizedNormalWS.x) >= abs(rasterizedNormalWS.z)
        ? float3(1.0, 0.0, 0.0)
        : float3(0.0, 0.0, 1.0);
    sideAxisWeights = lerp(
        fallbackSideAxis,
        sideAxisWeights,
        hasStableSideProjection);
    axisWeights = lerp(
        sideAxisWeights,
        float3(0.0, 1.0, 0.0),
        useTopProjection);

    // Material channels benefit from sharp, height-aware axis selection, but
    // applying those weights to projection-space normals snaps the lighting
    // toward a cardinal world axis. Linear geometric weights reconstruct the
    // terrain normal exactly when the layer normal strength is zero.
    float3 normalAxisWeights = abs(geometricNormalWS);
    normalAxisWeights /= max(dot(normalAxisWeights, 1.0), 1e-5);

    TS_LayerSurface surface;
    surface.albedo =
        axisX.albedo * axisWeights.x + axisY.albedo * axisWeights.y + axisZ.albedo * axisWeights.z;
    surface.height = dot(float3(axisX.height, axisY.height, axisZ.height), axisWeights);
    surface.normalWS = normalize(
        axisX.normalWS * normalAxisWeights.x +
        axisY.normalWS * normalAxisWeights.y +
        axisZ.normalWS * normalAxisWeights.z);
    surface.metallic = dot(float3(axisX.metallic, axisY.metallic, axisZ.metallic), axisWeights);
    surface.smoothness = dot(float3(axisX.smoothness, axisY.smoothness, axisZ.smoothness), axisWeights);
    surface.occlusion = dot(float3(axisX.occlusion, axisY.occlusion, axisZ.occlusion), axisWeights);
    return surface;
}

float4 TS_ApplyHeightBlend(TS_TopLayers top, float4 heights)
{
    float4 weights = top.weights;
    if (_TS_HeightBlendEnabled < 0.5)
    {
        return weights;
    }

    float4 offsets = float4(
        _TS_LayerHeightSurface[top.indices.x].x,
        _TS_LayerHeightSurface[top.indices.y].x,
        _TS_LayerHeightSurface[top.indices.z].x,
        _TS_LayerHeightSurface[top.indices.w].x) + _TS_HeightParameters.y;
    float4 contrasts = float4(
        _TS_LayerHeightSurface[top.indices.x].y,
        _TS_LayerHeightSurface[top.indices.y].y,
        _TS_LayerHeightSurface[top.indices.z].y,
        _TS_LayerHeightSurface[top.indices.w].y) * _TS_HeightParameters.z;
    heights = saturate((heights + offsets - 0.5) * contrasts + 0.5);
    float4 splatHeight = heights * weights;
    float maxHeight = max(splatHeight.x, max(splatHeight.y, max(splatHeight.z, splatHeight.w)));
    float4 weightedHeights = max(0.0, splatHeight + max(_TS_HeightParameters.x, 1e-5) - maxHeight);
    weightedHeights = (weightedHeights + 1e-6) * weights;
    return weightedHeights / max(dot(weightedHeights, 1.0), 1e-6);
}

float3 TS_Overlay(float3 baseColor, float3 blendColor)
{
    float3 low = 2.0 * baseColor * blendColor;
    float3 high = 1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor);
    return lerp(low, high, step(0.5, baseColor));
}

void TS_ApplyGlobalTexturing(inout TS_Surface surface, float3 positionWS, float3 geometricNormalWS, float cameraDistance)
{
    if (dot(_TS_GlobalFlags.xyz, 1.0) < 0.5)
    {
        return;
    }

    float2 globalUV = positionWS.xz * _TS_GlobalMapping.xy + _TS_GlobalMapping.zw;
    float fade = TS_SmoothRange(cameraDistance, _TS_GlobalFade.xy);
    float opacity = lerp(_TS_GlobalFadeOpacity.x, _TS_GlobalFadeOpacity.y, fade);

    float3 globalTint = 0.5;
    [branch]
    if (_TS_GlobalFlags.x > 0.5 || _TS_GlobalFlags.z > 0.5)
    {
        globalTint = SAMPLE_TEXTURE2D(_TS_GlobalTint, sampler_TS_GlobalTint, globalUV).rgb;
        float tintStrength = _TS_GlobalFlags.x * _TS_GlobalParameters.x * opacity;
        float3 tinted = surface.albedo * (globalTint * 2.0);
        if (_TS_GlobalFlags.w > 0.5 && _TS_GlobalFlags.w < 1.5)
        {
            tinted = TS_Overlay(surface.albedo, globalTint);
        }
        else if (_TS_GlobalFlags.w >= 1.5)
        {
            tinted = globalTint;
        }
        surface.albedo = lerp(surface.albedo, tinted, tintStrength);

        float replacement =
            _TS_GlobalFlags.z * _TS_GlobalReplacement.z *
            TS_SmoothRange(cameraDistance, _TS_GlobalReplacement.xy);
        surface.albedo = lerp(surface.albedo, globalTint, replacement);
    }

    [branch]
    if (_TS_GlobalFlags.y > 0.5)
    {
        float3 globalNormalTS = UnpackNormalScale(
            SAMPLE_TEXTURE2D(_TS_GlobalNormal, sampler_TS_GlobalNormal, globalUV),
            _TS_GlobalParameters.y);
        float3 globalNormalWS = TS_TransformTerrainNormal(globalNormalTS, geometricNormalWS);
        surface.normalWS = normalize(lerp(surface.normalWS, globalNormalWS, opacity));
    }
}

TS_Surface TS_BuildSurfaceFromTop(
    TS_TopLayers top,
    float3 positionWS,
    float2 planarPosition,
    float3 geometricNormalWS)
{
    float cameraDistance = distance(positionWS, GetCameraPositionWS());

    TS_LayerSurface layer0 = TS_SampleLayer(
        top.indices.x, positionWS, planarPosition, geometricNormalWS, cameraDistance);
    TS_ApplyLayerTint(top.indices.x, layer0);
    TS_LayerSurface layer1 = TS_SampleLayer(
        top.indices.y, positionWS, planarPosition, geometricNormalWS, cameraDistance);
    TS_ApplyLayerTint(top.indices.y, layer1);
    TS_LayerSurface layer2 = layer1;
    TS_LayerSurface layer3 = layer1;
    [branch]
    if (_TS_BlendQuality > 2.5)
    {
        layer2 = TS_SampleLayer(
            top.indices.z, positionWS, planarPosition, geometricNormalWS, cameraDistance);
        TS_ApplyLayerTint(top.indices.z, layer2);
    }
    [branch]
    if (_TS_BlendQuality > 3.5)
    {
        layer3 = TS_SampleLayer(
            top.indices.w, positionWS, planarPosition, geometricNormalWS, cameraDistance);
        TS_ApplyLayerTint(top.indices.w, layer3);
    }

    float4 weights = TS_ApplyHeightBlend(
        top,
        float4(layer0.height, layer1.height, layer2.height, layer3.height));
    TS_Surface surface;
    surface.albedo =
        layer0.albedo * weights.x + layer1.albedo * weights.y +
        layer2.albedo * weights.z + layer3.albedo * weights.w;
    surface.normalWS = normalize(
        layer0.normalWS * weights.x + layer1.normalWS * weights.y +
        layer2.normalWS * weights.z + layer3.normalWS * weights.w);
    surface.metallic = dot(
        float4(layer0.metallic, layer1.metallic, layer2.metallic, layer3.metallic), weights);
    surface.smoothness = dot(
        float4(layer0.smoothness, layer1.smoothness, layer2.smoothness, layer3.smoothness), weights);
    surface.occlusion = dot(
        float4(layer0.occlusion, layer1.occlusion, layer2.occlusion, layer3.occlusion), weights);

    TS_ApplyGlobalTexturing(surface, positionWS, geometricNormalWS, cameraDistance);
    return surface;
}

TS_Surface TS_BuildSurface(float2 terrainUV, float3 positionWS, float3 geometricNormalWS)
{
    float2 terrainLocalPosition = terrainUV * _TS_TerrainOriginSize.zw;
    return TS_BuildSurfaceFromTop(
        TS_SelectTopLayers(terrainUV),
        positionWS,
        terrainLocalPosition,
        geometricNormalWS);
}

#endif
