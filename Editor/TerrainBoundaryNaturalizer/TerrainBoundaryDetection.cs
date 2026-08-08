using System;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal static class TerrainBoundaryDetection
    {
        private const float StrongDominanceMinimum = 0.4f;
        private const float PairCoverageMinimum = 0.75f;
        private const float CloseWeightDifference = 0.25f;

        private static readonly Vector2Int[] CardinalNeighbors =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up
        };

        public static float[,] BuildBrushMask(
            TerrainBoundaryTileSnapshot tile,
            TerrainBoundaryStroke stroke,
            TerrainBoundaryNaturalizerSettings settings)
        {
            float[,] result = new float[tile.Height, tile.Width];
            for (int y = 0; y < tile.Height; y++)
            {
                for (int x = 0; x < tile.Width; x++)
                {
                    Vector3 world = tile.LocalGridToWorld(x, y);
                    result[y, x] = stroke.EvaluateMask(
                        new Vector2(world.x, world.z),
                        settings.BrushRadius,
                        settings.BrushFalloff);
                }
            }

            return result;
        }

        public static bool[,] BuildBoundaryMap(
            TerrainBoundaryTileSnapshot tile,
            TerrainBoundaryWorldSampler sampler,
            TerrainBoundaryNaturalizerSettings settings,
            int layerAIndex,
            int layerBIndex,
            Func<float, bool> cancelRequested)
        {
            int height = tile.Height;
            int width = tile.Width;
            bool[,] result = new bool[height, width];
            float[] neighborWeights = new float[tile.LayerCount];
            for (int y = 0; y < height; y++)
            {
                if ((y & 15) == 0
                    && cancelRequested != null
                    && cancelRequested(0.3f * y / Mathf.Max(1f, height)))
                {
                    throw new OperationCanceledException("Terrain boundary naturalization was cancelled.");
                }

                for (int x = 0; x < width; x++)
                {
                    if (IsSoftBoundary(
                            tile.Weights,
                            y,
                            x,
                            settings.LayerScope,
                            layerAIndex,
                            layerBIndex))
                    {
                        result[y, x] = true;
                        continue;
                    }

                    Vector3 world = tile.LocalGridToWorld(x, y);
                    for (int neighborIndex = 0; neighborIndex < CardinalNeighbors.Length; neighborIndex++)
                    {
                        Vector2Int direction = CardinalNeighbors[neighborIndex];
                        int nx = x + direction.x;
                        int ny = y + direction.y;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            if (DifferentDominantSides(
                                    tile.Weights,
                                    y,
                                    x,
                                    tile.Weights,
                                    ny,
                                    nx,
                                    settings.LayerScope,
                                    layerAIndex,
                                    layerBIndex))
                            {
                                result[y, x] = true;
                                break;
                            }
                            continue;
                        }

                        Vector2 neighborWorld = new(
                            world.x + direction.x * tile.MetersPerTexel.x,
                            world.z + direction.y * tile.MetersPerTexel.y);
                        if (!sampler.TrySample(neighborWorld, neighborWeights))
                        {
                            continue;
                        }

                        if (DifferentDominantSides(
                                tile.Weights,
                                y,
                                x,
                                neighborWeights,
                                settings.LayerScope,
                                layerAIndex,
                                layerBIndex))
                        {
                            result[y, x] = true;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        public static float[,] BuildBoundaryDistance(bool[,] boundary, Vector2 metersPerTexel)
        {
            int height = boundary.GetLength(0);
            int width = boundary.GetLength(1);
            float[,] distance = new float[height, width];
            float diagonal = Mathf.Sqrt(
                metersPerTexel.x * metersPerTexel.x + metersPerTexel.y * metersPerTexel.y);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    distance[y, x] = boundary[y, x] ? 0f : float.PositiveInfinity;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = distance[y, x];
                    if (x > 0) value = Mathf.Min(value, distance[y, x - 1] + metersPerTexel.x);
                    if (y > 0) value = Mathf.Min(value, distance[y - 1, x] + metersPerTexel.y);
                    if (x > 0 && y > 0) value = Mathf.Min(value, distance[y - 1, x - 1] + diagonal);
                    if (x + 1 < width && y > 0) value = Mathf.Min(value, distance[y - 1, x + 1] + diagonal);
                    distance[y, x] = value;
                }
            }

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    float value = distance[y, x];
                    if (x + 1 < width) value = Mathf.Min(value, distance[y, x + 1] + metersPerTexel.x);
                    if (y + 1 < height) value = Mathf.Min(value, distance[y + 1, x] + metersPerTexel.y);
                    if (x + 1 < width && y + 1 < height) value = Mathf.Min(value, distance[y + 1, x + 1] + diagonal);
                    if (x > 0 && y + 1 < height) value = Mathf.Min(value, distance[y + 1, x - 1] + diagonal);
                    distance[y, x] = value;
                }
            }

            return distance;
        }

        public static bool ContainsBoundary(bool[,] boundary)
        {
            foreach (bool value in boundary)
            {
                if (value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSoftBoundary(
            float[,,] weights,
            int y,
            int x,
            TerrainBoundaryLayerScope scope,
            int layerAIndex,
            int layerBIndex)
        {
            if (scope == TerrainBoundaryLayerScope.SelectedPair)
            {
                float a = weights[y, x, layerAIndex];
                float b = weights[y, x, layerBIndex];
                return a + b >= PairCoverageMinimum && Mathf.Abs(a - b) <= CloseWeightDifference;
            }

            TerrainBoundaryWeightUtility.FindTopTwo(
                weights,
                y,
                x,
                out _,
                out float first,
                out _,
                out float second);
            return first + second >= PairCoverageMinimum && first - second <= CloseWeightDifference;
        }

        private static bool DifferentDominantSides(
            float[,,] first,
            int firstY,
            int firstX,
            float[,,] second,
            int secondY,
            int secondX,
            TerrainBoundaryLayerScope scope,
            int layerAIndex,
            int layerBIndex)
        {
            if (scope == TerrainBoundaryLayerScope.SelectedPair)
            {
                float firstA = first[firstY, firstX, layerAIndex];
                float firstB = first[firstY, firstX, layerBIndex];
                float secondA = second[secondY, secondX, layerAIndex];
                float secondB = second[secondY, secondX, layerBIndex];
                return firstA + firstB >= PairCoverageMinimum
                    && secondA + secondB >= PairCoverageMinimum
                    && Mathf.Max(firstA, firstB) >= StrongDominanceMinimum
                    && Mathf.Max(secondA, secondB) >= StrongDominanceMinimum
                    && (firstA >= firstB) != (secondA >= secondB);
            }

            TerrainBoundaryWeightUtility.FindTopTwo(
                first,
                firstY,
                firstX,
                out int firstLayer,
                out float firstWeight,
                out _,
                out _);
            TerrainBoundaryWeightUtility.FindTopTwo(
                second,
                secondY,
                secondX,
                out int secondLayer,
                out float secondWeight,
                out _,
                out _);
            return firstLayer != secondLayer
                && firstWeight >= StrongDominanceMinimum
                && secondWeight >= StrongDominanceMinimum;
        }

        private static bool DifferentDominantSides(
            float[,,] first,
            int firstY,
            int firstX,
            float[] second,
            TerrainBoundaryLayerScope scope,
            int layerAIndex,
            int layerBIndex)
        {
            if (scope == TerrainBoundaryLayerScope.SelectedPair)
            {
                float firstA = first[firstY, firstX, layerAIndex];
                float firstB = first[firstY, firstX, layerBIndex];
                float secondA = second[layerAIndex];
                float secondB = second[layerBIndex];
                return firstA + firstB >= PairCoverageMinimum
                    && secondA + secondB >= PairCoverageMinimum
                    && Mathf.Max(firstA, firstB) >= StrongDominanceMinimum
                    && Mathf.Max(secondA, secondB) >= StrongDominanceMinimum
                    && (firstA >= firstB) != (secondA >= secondB);
            }

            TerrainBoundaryWeightUtility.FindTopTwo(
                first,
                firstY,
                firstX,
                out int firstLayer,
                out float firstWeight,
                out _,
                out _);
            TerrainBoundaryWeightUtility.FindTopTwo(
                second,
                out int secondLayer,
                out float secondWeight,
                out _,
                out _);
            return firstLayer != secondLayer
                && firstWeight >= StrongDominanceMinimum
                && secondWeight >= StrongDominanceMinimum;
        }
    }
}
