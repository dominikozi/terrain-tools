using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal static class TerrainBoundaryTopology
    {
        private static readonly Vector2Int[] Neighbors =
        {
            new(-1, -1), new(0, -1), new(1, -1),
            new(-1, 0),               new(1, 0),
            new(-1, 1),  new(0, 1),  new(1, 1)
        };

        public static void RemoveDetachedComponents(
            float[,,] original,
            float[,,] candidate,
            int[,] originalDominant)
        {
            int height = candidate.GetLength(0);
            int width = candidate.GetLength(1);
            int[,] candidateDominant = BuildDominantMap(candidate);
            bool[,] visited = new bool[height, width];
            Queue<int> queue = new();
            List<int> component = new();

            for (int startY = 0; startY < height; startY++)
            {
                for (int startX = 0; startX < width; startX++)
                {
                    if (visited[startY, startX])
                    {
                        continue;
                    }

                    int layer = candidateDominant[startY, startX];
                    bool anchored = false;
                    bool touchesSnapshotEdge = false;
                    component.Clear();
                    queue.Clear();
                    queue.Enqueue(startY * width + startX);
                    visited[startY, startX] = true;
                    while (queue.Count > 0)
                    {
                        int encoded = queue.Dequeue();
                        int y = encoded / width;
                        int x = encoded - y * width;
                        component.Add(encoded);
                        anchored |= originalDominant[y, x] == layer;
                        touchesSnapshotEdge |= x == 0 || y == 0 || x == width - 1 || y == height - 1;

                        for (int neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                        {
                            int nx = x + Neighbors[neighborIndex].x;
                            int ny = y + Neighbors[neighborIndex].y;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height
                                || visited[ny, nx]
                                || candidateDominant[ny, nx] != layer)
                            {
                                continue;
                            }

                            visited[ny, nx] = true;
                            queue.Enqueue(ny * width + nx);
                        }
                    }

                    if (anchored || touchesSnapshotEdge)
                    {
                        continue;
                    }

                    for (int i = 0; i < component.Count; i++)
                    {
                        int encoded = component[i];
                        int y = encoded / width;
                        int x = encoded - y * width;
                        CopyPixel(original, candidate, y, x);
                    }
                }
            }
        }

        public static void AddIslands(
            TerrainBoundaryTileSnapshot tile,
            float[,,] candidate,
            float[,] boundaryDistance,
            float[,] brushMask,
            TerrainBoundaryNaturalizerSettings settings,
            int layerAIndex,
            int layerBIndex)
        {
            if (settings.IslandAmount <= 0f)
            {
                return;
            }

            int height = candidate.GetLength(0);
            int width = candidate.GetLength(1);
            int[,] dominant = BuildDominantMap(candidate);
            bool[,] proposed = new bool[height, width];
            int[,] proposedSource = new int[height, width];
            int[,] proposedTarget = new int[height, width];
            float[,] proposalStrength = new float[height, width];
            float minimumGap = tile.MaximumMetersPerTexel * 1.1f;
            float effectiveSize = Mathf.Max(settings.IslandSize, tile.MaximumMetersPerTexel * 2f);
            float threshold = Mathf.Lerp(0.96f, 0.62f, settings.IslandAmount);
            int searchX = Mathf.Max(1, Mathf.CeilToInt(settings.IslandReach / tile.MetersPerTexel.x));
            int searchY = Mathf.Max(1, Mathf.CeilToInt(settings.IslandReach / tile.MetersPerTexel.y));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (brushMask[y, x] <= 0f
                        || boundaryDistance[y, x] <= minimumGap
                        || boundaryDistance[y, x] > settings.IslandReach)
                    {
                        continue;
                    }

                    int target = dominant[y, x];
                    int source;
                    if (settings.LayerScope == TerrainBoundaryLayerScope.SelectedPair)
                    {
                        source = settings.IslandSource == TerrainBoundaryIslandSource.LayerA
                            ? layerAIndex
                            : layerBIndex;
                        int requiredTarget = source == layerAIndex ? layerBIndex : layerAIndex;
                        if (target != requiredTarget)
                        {
                            continue;
                        }
                    }
                    else if (!TryFindNearestDifferentLayer(
                                 dominant,
                                 x,
                                 y,
                                 target,
                                 searchX,
                                 searchY,
                                 tile.MetersPerTexel,
                                 settings.IslandReach,
                                 out source))
                    {
                        continue;
                    }

                    if (source < 0 || source == target
                        || candidate[y, x, source] + candidate[y, x, target] < 0.65f
                        || HasAdjacentLayer(dominant, x, y, source))
                    {
                        continue;
                    }

                    Vector3 world = tile.LocalGridToWorld(x, y);
                    int pairChannel = 701 + source * 37 + target * 19;
                    float fine = TerrainBoundaryTerrainUtility.SampleSignedDomainWarpedNoise(
                            world,
                            effectiveSize,
                            settings.Seed + 409,
                            pairChannel)
                        * 0.5f + 0.5f;
                    float broad = TerrainBoundaryTerrainUtility.SampleSignedDomainWarpedNoise(
                            world,
                            effectiveSize * 2.35f,
                            settings.Seed - 233,
                            pairChannel + 11)
                        * 0.5f + 0.5f;
                    float blob = fine * 0.78f + broad * 0.22f;
                    if (blob <= threshold)
                    {
                        continue;
                    }

                    proposed[y, x] = true;
                    proposedSource[y, x] = source;
                    proposedTarget[y, x] = target;
                    proposalStrength[y, x] = Mathf.InverseLerp(threshold, 1f, blob);
                }
            }

            ApplyAcceptedIslandComponents(
                candidate,
                dominant,
                proposed,
                proposedSource,
                proposedTarget,
                proposalStrength,
                brushMask,
                settings.EdgeContrast);
        }

        public static int[,] BuildDominantMap(float[,,] weights)
        {
            int height = weights.GetLength(0);
            int width = weights.GetLength(1);
            int[,] result = new int[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[y, x] = TerrainBoundaryWeightUtility.FindDominant(weights, y, x);
                }
            }

            return result;
        }

        private static void ApplyAcceptedIslandComponents(
            float[,,] candidate,
            int[,] mainDominant,
            bool[,] proposed,
            int[,] proposedSource,
            int[,] proposedTarget,
            float[,] proposalStrength,
            float[,] brushMask,
            float edgeContrast)
        {
            int height = candidate.GetLength(0);
            int width = candidate.GetLength(1);
            int layerCount = candidate.GetLength(2);
            bool[,] visited = new bool[height, width];
            Queue<int> queue = new();
            List<int> component = new();
            float[] targetWeights = new float[layerCount];

            for (int startY = 0; startY < height; startY++)
            {
                for (int startX = 0; startX < width; startX++)
                {
                    if (!proposed[startY, startX] || visited[startY, startX])
                    {
                        continue;
                    }

                    int source = proposedSource[startY, startX];
                    int target = proposedTarget[startY, startX];
                    bool touchesMainSource = false;
                    component.Clear();
                    queue.Clear();
                    queue.Enqueue(startY * width + startX);
                    visited[startY, startX] = true;
                    while (queue.Count > 0)
                    {
                        int encoded = queue.Dequeue();
                        int y = encoded / width;
                        int x = encoded - y * width;
                        component.Add(encoded);

                        for (int neighborIndex = 0; neighborIndex < Neighbors.Length; neighborIndex++)
                        {
                            int nx = x + Neighbors[neighborIndex].x;
                            int ny = y + Neighbors[neighborIndex].y;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            {
                                continue;
                            }

                            if (mainDominant[ny, nx] == source && !proposed[ny, nx])
                            {
                                touchesMainSource = true;
                            }

                            if (!visited[ny, nx]
                                && proposed[ny, nx]
                                && proposedSource[ny, nx] == source
                                && proposedTarget[ny, nx] == target)
                            {
                                visited[ny, nx] = true;
                                queue.Enqueue(ny * width + nx);
                            }
                        }
                    }

                    if (component.Count < 2 || touchesMainSource)
                    {
                        continue;
                    }

                    for (int i = 0; i < component.Count; i++)
                    {
                        int encoded = component[i];
                        int y = encoded / width;
                        int x = encoded - y * width;
                        TerrainBoundaryWeightUtility.CopyPixel(candidate, y, x, targetWeights);
                        float pairTotal = targetWeights[source] + targetWeights[target];
                        float desiredSourceRatio = Mathf.Lerp(0.78f, 0.96f, proposalStrength[y, x]);
                        targetWeights[source] = pairTotal * desiredSourceRatio;
                        targetWeights[target] = pairTotal * (1f - desiredSourceRatio);
                        TerrainBoundaryWeightUtility.ApplyPairContrast(
                            targetWeights,
                            source,
                            target,
                            edgeContrast);
                        TerrainBoundaryWeightUtility.Normalize(targetWeights);
                        BlendCurrentPixel(candidate, y, x, targetWeights, brushMask[y, x]);
                    }
                }
            }
        }

        private static bool TryFindNearestDifferentLayer(
            int[,] dominant,
            int centerX,
            int centerY,
            int currentLayer,
            int searchX,
            int searchY,
            Vector2 metersPerTexel,
            float maximumDistance,
            out int layer)
        {
            layer = -1;
            float closestSquared = maximumDistance * maximumDistance;
            int height = dominant.GetLength(0);
            int width = dominant.GetLength(1);
            int minX = Mathf.Max(0, centerX - searchX);
            int maxX = Mathf.Min(width - 1, centerX + searchX);
            int minY = Mathf.Max(0, centerY - searchY);
            int maxY = Mathf.Min(height - 1, centerY + searchY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int candidateLayer = dominant[y, x];
                    if (candidateLayer == currentLayer)
                    {
                        continue;
                    }

                    float dx = (x - centerX) * metersPerTexel.x;
                    float dy = (y - centerY) * metersPerTexel.y;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < closestSquared)
                    {
                        closestSquared = distanceSquared;
                        layer = candidateLayer;
                    }
                }
            }

            return layer >= 0;
        }

        private static bool HasAdjacentLayer(int[,] dominant, int centerX, int centerY, int layer)
        {
            int height = dominant.GetLength(0);
            int width = dominant.GetLength(1);
            for (int i = 0; i < Neighbors.Length; i++)
            {
                int x = centerX + Neighbors[i].x;
                int y = centerY + Neighbors[i].y;
                if (x >= 0 && x < width && y >= 0 && y < height && dominant[y, x] == layer)
                {
                    return true;
                }
            }

            return false;
        }

        private static void BlendCurrentPixel(
            float[,,] candidate,
            int y,
            int x,
            float[] target,
            float blend)
        {
            int layerCount = candidate.GetLength(2);
            float clampedBlend = Mathf.Clamp01(blend);
            for (int layer = 0; layer < layerCount; layer++)
            {
                candidate[y, x, layer] = Mathf.Lerp(
                    candidate[y, x, layer],
                    target[layer],
                    clampedBlend);
            }

            TerrainBoundaryWeightUtility.Normalize(candidate, y, x);
        }

        private static void CopyPixel(float[,,] source, float[,,] destination, int y, int x)
        {
            int layerCount = source.GetLength(2);
            for (int layer = 0; layer < layerCount; layer++)
            {
                destination[y, x, layer] = source[y, x, layer];
            }
        }
    }
}
