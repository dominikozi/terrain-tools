#ifndef DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_STOCHASTIC_INCLUDED
#define DOMINIKOZI_TERRAIN_TOOLS_TERRAIN_SURFACE_STOCHASTIC_INCLUDED

// A triangle-grid stochastic sampler in the same family as MicroSplat's
// stochastic mode. Each triangle vertex selects a stable random UV transform;
// the three complete PBR samples are then blended using barycentric weights.

struct TS_TextureSetSample
{
    float4 albedoHeight;
    float4 normalSurface;
    float metallic;
};

struct TS_StochasticCell
{
    float3 weights;
    float2 vertex0;
    float2 vertex1;
    float2 vertex2;
};

float2 TS_Rotate2D(float2 value, float2 cosineSine)
{
    return float2(
        cosineSine.x * value.x - cosineSine.y * value.y,
        cosineSine.y * value.x + cosineSine.x * value.y);
}

float2 TS_InverseRotate2D(float2 value, float2 cosineSine)
{
    return float2(
        cosineSine.x * value.x + cosineSine.y * value.y,
        -cosineSine.y * value.x + cosineSine.x * value.y);
}

float3 TS_StochasticHash(float2 latticeVertex, int layerIndex)
{
    float seed = _TS_StochasticExtra.x + (float)layerIndex * 37.0;
    float3 position = float3(latticeVertex, seed);
    return frac(sin(float3(
        dot(position, float3(127.1, 311.7, 74.7)),
        dot(position, float3(269.5, 183.3, 246.1)),
        dot(position, float3(113.5, 271.9, 124.6)))) * 43758.5453123);
}

TS_StochasticCell TS_GetStochasticCell(float2 gridUV)
{
    // 2 * sqrt(3) produces compact, near-equilateral stochastic cells.
    float2 scaled = gridUV * (_TS_StochasticParameters.y * 3.46410161514);
    // This is the same simplex skew used by MicroSplat's TriangleGrid.
    float2 skewed = float2(
        scaled.x,
        -scaled.x * 0.57735026919 + scaled.y * 1.15470053838);
    float2 cell = floor(skewed);
    float2 localCoordinates = frac(skewed);

    TS_StochasticCell cellData;
    if (localCoordinates.x + localCoordinates.y <= 1.0)
    {
        cellData.weights = float3(
            1.0 - localCoordinates.x - localCoordinates.y,
            localCoordinates.x,
            localCoordinates.y);
        cellData.vertex0 = cell;
        cellData.vertex1 = cell + float2(1.0, 0.0);
        cellData.vertex2 = cell + float2(0.0, 1.0);
    }
    else
    {
        cellData.weights = float3(
            localCoordinates.x + localCoordinates.y - 1.0,
            1.0 - localCoordinates.x,
            1.0 - localCoordinates.y);
        cellData.vertex0 = cell + float2(1.0, 1.0);
        cellData.vertex1 = cell + float2(0.0, 1.0);
        cellData.vertex2 = cell + float2(1.0, 0.0);
    }

    return cellData;
}

TS_TextureSetSample TS_SampleTextureSetRegular(
    int layerIndex,
    float2 uv,
    float2 uvDx,
    float2 uvDy)
{
    TS_TextureSetSample textureSample;
    textureSample.albedoHeight = TS_SampleAlbedoHeight(layerIndex, uv, uvDx, uvDy);
    textureSample.normalSurface = TS_SampleNormalSurface(layerIndex, uv, uvDx, uvDy);
    textureSample.metallic = TS_SampleMetallic(layerIndex, uv, uvDx, uvDy);
    return textureSample;
}

TS_TextureSetSample TS_SampleTextureSetTransformed(
    int layerIndex,
    float2 uv,
    float2 uvDx,
    float2 uvDy,
    float2 latticeVertex)
{
    float3 randomValues = TS_StochasticHash(latticeVertex, layerIndex);
    float angle = 0.0;
    if (_TS_StochasticExtra.y > 0.5)
    {
        angle = floor(randomValues.z * 4.0) * 1.57079632679;
    }

    float2 cosineSine = float2(cos(angle), sin(angle));
    float2 transformedUV = TS_Rotate2D(uv, cosineSine) + randomValues.xy;
    float2 transformedDx = TS_Rotate2D(uvDx, cosineSine);
    float2 transformedDy = TS_Rotate2D(uvDy, cosineSine);
    TS_TextureSetSample textureSample = TS_SampleTextureSetRegular(
        layerIndex,
        transformedUV,
        transformedDx,
        transformedDy);

    // The UV rotation also rotates tangent space. Transform XY back before
    // blending, otherwise normal-map lighting would visibly rotate per cell.
    float3 normalTS = TS_DecodeNormal(textureSample.normalSurface.rg, 1.0);
    normalTS.xy = TS_InverseRotate2D(normalTS.xy, cosineSine);
    textureSample.normalSurface.rg = normalTS.xy * 0.5 + 0.5;
    return textureSample;
}

float3 TS_GetStochasticBlendWeights(float3 spatialWeights, float3 heights)
{
    float contrast = max(_TS_StochasticParameters.z, 0.0001);
    float3 weights = pow(max(spatialWeights, 1e-6), contrast);
    weights /= max(dot(weights, 1.0), 1e-6);

    if (_TS_StochasticExtra.z > 0.5)
    {
        // Equivalent shape to MicroSplat's BaryWeightBlend: sampled height
        // modulates each barycentric weight, then a relative transition band
        // keeps only samples close to the strongest result.
        const float epsilon = 1.0 / 1024.0;
        float3 heightWeights = weights * (saturate(heights) + epsilon);
        float maxWeight = max(heightWeights.x, max(heightWeights.y, heightWeights.z));
        float transition = max(_TS_StochasticParameters.w * maxWeight, 1e-5);
        float threshold = maxWeight - transition;
        weights = saturate((heightWeights - threshold) / transition);
        weights /= max(dot(weights, 1.0), 1e-6);
    }

    return weights;
}

TS_TextureSetSample TS_SampleTextureSetStochastic(
    int layerIndex,
    float2 uv,
    float2 uvDx,
    float2 uvDy,
    float2 gridUV)
{
    TS_StochasticCell cellData = TS_GetStochasticCell(gridUV);
    TS_TextureSetSample sample0 = TS_SampleTextureSetTransformed(
        layerIndex, uv, uvDx, uvDy, cellData.vertex0);
    TS_TextureSetSample sample1 = TS_SampleTextureSetTransformed(
        layerIndex, uv, uvDx, uvDy, cellData.vertex1);
    TS_TextureSetSample sample2 = TS_SampleTextureSetTransformed(
        layerIndex, uv, uvDx, uvDy, cellData.vertex2);

    float3 weights = TS_GetStochasticBlendWeights(
        cellData.weights,
        float3(sample0.albedoHeight.a, sample1.albedoHeight.a, sample2.albedoHeight.a));

    TS_TextureSetSample result;
    result.albedoHeight =
        sample0.albedoHeight * weights.x +
        sample1.albedoHeight * weights.y +
        sample2.albedoHeight * weights.z;

    float3 normal0 = TS_DecodeNormal(sample0.normalSurface.rg, 1.0);
    float3 normal1 = TS_DecodeNormal(sample1.normalSurface.rg, 1.0);
    float3 normal2 = TS_DecodeNormal(sample2.normalSurface.rg, 1.0);
    float3 blendedNormal = normalize(
        normal0 * weights.x + normal1 * weights.y + normal2 * weights.z);
    result.normalSurface = float4(
        blendedNormal.xy * 0.5 + 0.5,
        dot(float3(sample0.normalSurface.b, sample1.normalSurface.b, sample2.normalSurface.b), weights),
        dot(float3(sample0.normalSurface.a, sample1.normalSurface.a, sample2.normalSurface.a), weights));
    result.metallic = dot(float3(sample0.metallic, sample1.metallic, sample2.metallic), weights);
    return result;
}

TS_TextureSetSample TS_SampleTextureSet(
    int layerIndex,
    float2 uv,
    float2 uvDx,
    float2 uvDy,
    float2 gridUV)
{
    [branch]
    if (_TS_StochasticParameters.x > 0.5 && _TS_LayerSurfaceExtra[layerIndex].z > 0.5)
    {
        return TS_SampleTextureSetStochastic(layerIndex, uv, uvDx, uvDy, gridUV);
    }

    return TS_SampleTextureSetRegular(layerIndex, uv, uvDx, uvDy);
}

#endif
