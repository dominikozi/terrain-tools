using System;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal static class TerrainBoundaryNaturalizerProcessor
    {
        private const float ChangeEpsilon = 0.000001f;

        public static TerrainBoundaryTileResult Process(
            TerrainBoundaryTileSnapshot tile,
            TerrainBoundaryWorldSampler sampler,
            TerrainBoundaryStroke stroke,
            TerrainBoundaryNaturalizerSettings settings,
            int layerAIndex,
            int layerBIndex,
            Func<float, bool> cancelRequested = null)
        {
            if (tile == null || sampler == null || stroke == null || stroke.PointCount == 0 || settings == null)
            {
                return null;
            }

            int height = tile.Height;
            int width = tile.Width;
            int layerCount = tile.LayerCount;
            if (height <= 0 || width <= 0 || layerCount <= 1)
            {
                return null;
            }

            if (settings.LayerScope == TerrainBoundaryLayerScope.SelectedPair
                && (layerAIndex < 0 || layerBIndex < 0 || layerAIndex == layerBIndex
                    || layerAIndex >= layerCount || layerBIndex >= layerCount))
            {
                return null;
            }

            int[,] originalDominant = TerrainBoundaryTopology.BuildDominantMap(tile.Weights);
            float[,] brushMask = TerrainBoundaryDetection.BuildBrushMask(tile, stroke, settings);
            bool[,] boundary = TerrainBoundaryDetection.BuildBoundaryMap(
                tile,
                sampler,
                settings,
                layerAIndex,
                layerBIndex,
                cancelRequested);
            if (!TerrainBoundaryDetection.ContainsBoundary(boundary))
            {
                return null;
            }

            float[,] boundaryDistance = TerrainBoundaryDetection.BuildBoundaryDistance(
                boundary,
                tile.MetersPerTexel);
            float bandRadius = settings.MaximumDisplacement + tile.MaximumMetersPerTexel * 2f;
            if (settings.Character == TerrainBoundaryCharacter.Islands)
            {
                bandRadius = Mathf.Max(
                    bandRadius,
                    settings.IslandReach + settings.IslandSize + tile.MaximumMetersPerTexel * 2f);
            }

            float[,,] candidate = (float[,,])tile.Weights.Clone();
            float[] sampled = new float[layerCount];
            float[] target = new float[layerCount];
            for (int y = 0; y < height; y++)
            {
                if ((y & 15) == 0
                    && cancelRequested != null
                    && cancelRequested(0.35f + 0.4f * y / Mathf.Max(1f, height)))
                {
                    throw new OperationCanceledException("Terrain boundary naturalization was cancelled.");
                }

                for (int x = 0; x < width; x++)
                {
                    float blend = brushMask[y, x];
                    if (blend <= 0f || boundaryDistance[y, x] > bandRadius)
                    {
                        continue;
                    }

                    Vector3 world = tile.LocalGridToWorld(x, y);
                    Vector2 offset = EvaluateNoiseOffset(world, tile.MaximumMetersPerTexel, settings);
                    if (!sampler.TrySample(new Vector2(world.x + offset.x, world.z + offset.y), sampled))
                    {
                        continue;
                    }

                    TerrainBoundaryWeightUtility.CopyPixel(tile.Weights, y, x, target);
                    if (settings.LayerScope == TerrainBoundaryLayerScope.Auto)
                    {
                        Array.Copy(sampled, target, layerCount);
                        TerrainBoundaryWeightUtility.FindTopTwo(
                            target,
                            out int first,
                            out _,
                            out int second,
                            out _);
                        TerrainBoundaryWeightUtility.ApplyPairContrast(
                            target,
                            first,
                            second,
                            settings.EdgeContrast);
                    }
                    else
                    {
                        float sampledPairTotal = sampled[layerAIndex] + sampled[layerBIndex];
                        if (sampledPairTotal <= 0.000001f)
                        {
                            continue;
                        }

                        float sampledRatioA = sampled[layerAIndex] / sampledPairTotal;
                        TerrainBoundaryWeightUtility.SetPairRatio(
                            target,
                            layerAIndex,
                            layerBIndex,
                            sampledRatioA);
                        TerrainBoundaryWeightUtility.ApplyPairContrast(
                            target,
                            layerAIndex,
                            layerBIndex,
                            settings.EdgeContrast);
                    }

                    TerrainBoundaryWeightUtility.Normalize(target);
                    TerrainBoundaryWeightUtility.BlendPixel(
                        tile.Weights,
                        candidate,
                        y,
                        x,
                        target,
                        blend);
                }
            }

            TerrainBoundaryTopology.RemoveDetachedComponents(
                tile.Weights,
                candidate,
                originalDominant);
            if (settings.Character == TerrainBoundaryCharacter.Islands)
            {
                TerrainBoundaryTopology.AddIslands(
                    tile,
                    candidate,
                    boundaryDistance,
                    brushMask,
                    settings,
                    layerAIndex,
                    layerBIndex);
            }

            if (cancelRequested != null && cancelRequested(0.95f))
            {
                throw new OperationCanceledException("Terrain boundary naturalization was cancelled.");
            }

            return CropChangedResult(tile, candidate);
        }

        internal static Vector2 EvaluateNoiseOffset(
            Vector3 worldPosition,
            float maximumMetersPerTexel,
            TerrainBoundaryNaturalizerSettings settings)
        {
            float minimumFeatureSize = Mathf.Max(0.001f, maximumMetersPerTexel * 2f);
            Vector2 offset = Vector2.zero;
            AddNoiseBand(
                ref offset,
                worldPosition,
                Mathf.Max(settings.LargeFeatureSize, minimumFeatureSize),
                settings.LargeDisplacement,
                settings.Seed,
                101,
                107);
            AddNoiseBand(
                ref offset,
                worldPosition,
                Mathf.Max(settings.MediumFeatureSize, minimumFeatureSize),
                settings.MediumDisplacement,
                settings.Seed + 193,
                211,
                223);
            AddNoiseBand(
                ref offset,
                worldPosition,
                Mathf.Max(settings.SmallFeatureSize, minimumFeatureSize),
                settings.SmallDisplacement,
                settings.Seed - 317,
                307,
                311);
            return offset;
        }

        private static void AddNoiseBand(
            ref Vector2 offset,
            Vector3 worldPosition,
            float featureSize,
            float displacement,
            int seed,
            int xChannel,
            int yChannel)
        {
            if (displacement <= 0f)
            {
                return;
            }

            offset.x += TerrainBoundaryTerrainUtility.SampleSignedDomainWarpedNoise(
                worldPosition,
                featureSize,
                seed,
                xChannel) * displacement;
            offset.y += TerrainBoundaryTerrainUtility.SampleSignedDomainWarpedNoise(
                worldPosition,
                featureSize,
                seed,
                yChannel) * displacement;
        }

        private static TerrainBoundaryTileResult CropChangedResult(
            TerrainBoundaryTileSnapshot tile,
            float[,,] candidate)
        {
            int minX = tile.Width;
            int minY = tile.Height;
            int maxX = -1;
            int maxY = -1;
            int changedTexelCount = 0;
            for (int y = 0; y < tile.Height; y++)
            {
                for (int x = 0; x < tile.Width; x++)
                {
                    if (!TerrainBoundaryWeightUtility.PixelsDiffer(
                            tile.Weights,
                            candidate,
                            y,
                            x,
                            ChangeEpsilon))
                    {
                        continue;
                    }

                    changedTexelCount++;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return null;
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int layerCount = tile.LayerCount;
            float[,,] cropped = new float[height, width, layerCount];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        cropped[y, x, layer] = candidate[minY + y, minX + x, layer];
                    }
                }
            }

            return new TerrainBoundaryTileResult(
                tile.Terrain,
                tile.Rect.X + minX,
                tile.Rect.Y + minY,
                cropped,
                changedTexelCount);
        }
    }
}
