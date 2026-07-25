#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

internal static class DetailTerrainPaintUtility
{
    private const float MinBrushSize = 0.25f;
    private const float CoverageThresholdWidth = 0.15f;

    public static bool ValidatePreset(
        TerrainData terrainData,
        DetailTerrainPaintPreset preset,
        out string error)
    {
        return TryBuildPaintEntries(terrainData, preset, out _, out error);
    }

    public static void PaintAt(
        Terrain terrain,
        DetailTerrainPaintPreset paintPreset,
        Vector3 worldPosition,
        float brushSize,
        float brushStrength,
        float brushFalloff,
        float targetDensity,
        bool eraseMode)
    {
        if (terrain == null || terrain.terrainData == null || paintPreset == null || paintPreset.Entries.Count == 0)
        {
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData.detailWidth <= 0 || terrainData.detailHeight <= 0)
        {
            return;
        }

        if (!TryBuildPaintEntries(terrainData, paintPreset, out List<PaintEntry> paintEntries, out string error))
        {
            throw new InvalidOperationException(error);
        }

        if (paintEntries.Count == 0)
        {
            return;
        }

        if (!WorldToDetailCoord(terrain, worldPosition, out Vector2 center))
        {
            return;
        }

        float radius = Mathf.Max(MinBrushSize, brushSize) * 0.5f;
        Vector3 worldSize = Vector3.Scale(terrainData.size, terrain.transform.lossyScale);
        float worldWidth = Mathf.Max(0.001f, Mathf.Abs(worldSize.x));
        float worldLength = Mathf.Max(0.001f, Mathf.Abs(worldSize.z));

        int radiusPixelsX = Mathf.CeilToInt(radius / worldWidth * (terrainData.detailWidth - 1)) + 1;
        int radiusPixelsY = Mathf.CeilToInt(radius / worldLength * (terrainData.detailHeight - 1)) + 1;

        int minX = Mathf.Clamp(Mathf.FloorToInt(center.x) - radiusPixelsX, 0, terrainData.detailWidth - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(center.x) + radiusPixelsX, 0, terrainData.detailWidth - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(center.y) - radiusPixelsY, 0, terrainData.detailHeight - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(center.y) + radiusPixelsY, 0, terrainData.detailHeight - 1);

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        PaintRect(terrain, terrainData, paintEntries, worldPosition, radius, targetDensity, brushStrength, brushFalloff, eraseMode, minX, minY, width, height);
    }

    private static void PaintRect(
        Terrain terrain,
        TerrainData terrainData,
        IReadOnlyList<PaintEntry> paintEntries,
        Vector3 worldPosition,
        float radius,
        float targetDensity,
        float brushStrength,
        float brushFalloff,
        bool eraseMode,
        int minX,
        int minY,
        int width,
        int height)
    {
        int maximumDetailCount = Mathf.Max(1, terrainData.maxDetailScatterPerRes);
        int layerCount = paintEntries.Count;
        int[] layerIndices = new int[layerCount];
        int[] paintAmounts = new int[layerCount];
        int[][,] detailLayers = new int[layerCount][,];
        bool[] changedLayers = new bool[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            layerIndices[i] = paintEntries[i].PrototypeIndex;
            detailLayers[i] = terrainData.GetDetailLayer(minX, minY, width, height, layerIndices[i]);
        }

        Vector3 brushCenter = worldPosition;
        bool changed = false;

        for (int localY = 0; localY < height; localY++)
        {
            int mapY = minY + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int mapX = minX + localX;
                Vector3 sampleWorld = DetailCoordToWorld(terrain, mapX, mapY);
                float distance = HorizontalDistance(brushCenter, sampleWorld);
                if (distance > radius)
                {
                    continue;
                }

                float falloff = ComputeBrushFalloff(distance, radius, brushFalloff);
                float opacity = Mathf.Clamp01(brushStrength * falloff);
                if (opacity <= 0f)
                {
                    continue;
                }

                BuildPaintAmounts(
                    paintEntries,
                    sampleWorld,
                    mapX,
                    mapY,
                    Mathf.Clamp01(targetDensity),
                    maximumDetailCount,
                    paintAmounts);

                for (int i = 0; i < layerCount; i++)
                {
                    int currentValue = detailLayers[i][localY, localX];
                    int paintAmount = paintAmounts[i];
                    if (paintAmount <= 0)
                    {
                        continue;
                    }

                    int newValue = ApplyPaintAmount(currentValue, paintAmount, maximumDetailCount, opacity, eraseMode);
                    if (newValue == currentValue)
                    {
                        continue;
                    }

                    detailLayers[i][localY, localX] = newValue;
                    changedLayers[i] = true;
                    changed = true;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        for (int i = 0; i < layerCount; i++)
        {
            if (!changedLayers[i])
            {
                continue;
            }

            terrainData.SetDetailLayer(minX, minY, layerIndices[i], detailLayers[i]);
        }

        EditorUtility.SetDirty(terrainData);
        terrain.Flush();
        SceneView.RepaintAll();
    }

    private static bool TryBuildPaintEntries(
        TerrainData terrainData,
        DetailTerrainPaintPreset paintPreset,
        out List<PaintEntry> paintEntries,
        out string error)
    {
        paintEntries = new List<PaintEntry>();
        HashSet<int> usedPrototypeIndices = new();
        if (paintPreset == null)
        {
            error = "Assign a Detail Paint Preset.";
            return false;
        }

        foreach (DetailTerrainPaintPreset.Entry entry in paintPreset.Entries)
        {
            if (!entry.enabled)
            {
                continue;
            }

            if (!TerrainPrototypeResolver.TryResolveDetail(
                    terrainData,
                    entry.prefab,
                    entry.texture,
                    out int prototypeIndex,
                    out error))
            {
                return false;
            }

            if (!usedPrototypeIndices.Add(prototypeIndex))
            {
                UnityEngine.Object source = entry.prefab != null ? entry.prefab : entry.texture;
                error = $"Detail source '{source.name}' is assigned to more than one enabled preset entry.";
                return false;
            }

            if (entry.weight <= 0f)
            {
                continue;
            }

            paintEntries.Add(new PaintEntry(
                prototypeIndex,
                Mathf.Max(0f, entry.weight),
                Mathf.Clamp01(entry.coverage),
                Mathf.Max(0.001f, entry.noiseScale),
                Mathf.Clamp01(entry.noiseInfluence),
                entry.seed));
        }

        if (paintEntries.Count == 0)
        {
            error = "At least one enabled detail entry must have a weight greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool WorldToDetailCoord(Terrain terrain, Vector3 worldPosition, out Vector2 detailCoord)
    {
        detailCoord = default;

        if (!WorldToTerrainNormalized(terrain, worldPosition, out Vector2 normalized))
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        detailCoord = new Vector2(
            normalized.x * (terrainData.detailWidth - 1),
            normalized.y * (terrainData.detailHeight - 1));

        return true;
    }

    private static bool WorldToTerrainNormalized(Terrain terrain, Vector3 worldPosition, out Vector2 normalized)
    {
        normalized = default;
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainSize = terrainData.size;
        if (terrainSize.x <= 0f || terrainSize.z <= 0f)
        {
            return false;
        }

        Vector3 localPosition = terrain.transform.InverseTransformPoint(worldPosition);
        normalized = new Vector2(localPosition.x / terrainSize.x, localPosition.z / terrainSize.z);

        return normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f;
    }

    private static void BuildPaintAmounts(
        IReadOnlyList<PaintEntry> paintEntries,
        Vector3 worldPosition,
        int detailX,
        int detailY,
        float targetDensity,
        int maximumDetailCount,
        int[] paintAmounts)
    {
        Array.Clear(paintAmounts, 0, paintAmounts.Length);
        if (targetDensity <= 0f)
        {
            return;
        }

        float totalTarget = Mathf.Clamp01(targetDensity) * maximumDetailCount;
        for (int i = 0; i < paintEntries.Count; i++)
        {
            PaintEntry entry = paintEntries[i];
            float entryDensity = Mathf.Clamp01(entry.Weight * ComputeEntryMask(entry, worldPosition));
            if (entryDensity <= 0f)
            {
                continue;
            }

            float exactTarget = totalTarget * entryDensity;
            paintAmounts[i] = DitherDetailCount(exactTarget, detailX, detailY, paintEntries[i].Seed);
        }
    }

    internal static int DitherDetailCount(float exactTarget, int detailX, int detailY, int seed)
    {
        int baseCount = Mathf.FloorToInt(exactTarget);
        float fraction = exactTarget - baseCount;
        if (fraction <= 0f)
        {
            return baseCount;
        }

        float random = Hash01(detailX, detailY, seed);
        return random < fraction ? baseCount + 1 : baseCount;
    }

    internal static int ApplyPaintAmount(
        int currentValue,
        int paintAmount,
        int maximumDetailCount,
        float opacity,
        bool eraseMode)
    {
        int clampedCurrent = Mathf.Max(0, currentValue);
        int appliedAmount = Mathf.RoundToInt(Mathf.Max(0, paintAmount) * Mathf.Clamp01(opacity));
        if (appliedAmount <= 0)
        {
            appliedAmount = 1;
        }

        int signedAmount = eraseMode ? -appliedAmount : appliedAmount;
        return Mathf.Clamp(clampedCurrent + signedAmount, 0, Mathf.Max(1, maximumDetailCount));
    }

    private static Vector3 DetailCoordToWorld(Terrain terrain, int detailX, int detailY)
    {
        TerrainData terrainData = terrain.terrainData;
        float normalizedX = terrainData.detailWidth > 1
            ? detailX / (float)(terrainData.detailWidth - 1)
            : 0f;
        float normalizedY = terrainData.detailHeight > 1
            ? detailY / (float)(terrainData.detailHeight - 1)
            : 0f;

        Vector3 localPosition = new(
            normalizedX * terrainData.size.x,
            terrainData.GetInterpolatedHeight(normalizedX, normalizedY),
            normalizedY * terrainData.size.z);

        return terrain.transform.TransformPoint(localPosition);
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static float ComputeBrushFalloff(float distance, float radius, float falloff)
    {
        if (radius <= 0f || distance > radius)
        {
            return 0f;
        }

        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float clampedFalloff = Mathf.Clamp01(falloff);
        if (clampedFalloff <= 0.001f)
        {
            return 1f;
        }

        float innerDistance = 1f - clampedFalloff;
        if (normalizedDistance <= innerDistance)
        {
            return 1f;
        }

        float edgeT = 1f - Mathf.InverseLerp(innerDistance, 1f, normalizedDistance);
        return Mathf.SmoothStep(0f, 1f, edgeT);
    }

    private static float ComputeEntryMask(PaintEntry entry, Vector3 worldPosition)
    {
        if (entry.Coverage <= 0f)
        {
            return 0f;
        }

        if (entry.Coverage >= 1f)
        {
            return 1f;
        }

        float noise = ComputeNoise(worldPosition, entry.NoiseScale, entry.Seed);
        float patchMask = 1f - Mathf.SmoothStep(
            Mathf.Clamp01(entry.Coverage - CoverageThresholdWidth),
            Mathf.Clamp01(entry.Coverage + CoverageThresholdWidth),
            noise);

        return Mathf.Clamp01(Mathf.Lerp(entry.Coverage, patchMask, entry.NoiseInfluence));
    }

    private static float ComputeNoise(Vector3 worldPosition, float noiseScale, int seed)
    {
        float scale = Mathf.Max(0.001f, noiseScale);
        float offsetX = seed * 37.719f + 19.19f;
        float offsetY = seed * 11.131f - 73.73f;

        return Mathf.PerlinNoise(
            worldPosition.x / scale + offsetX,
            worldPosition.z / scale + offsetY);
    }

    private static float Hash01(int x, int y, int seed)
    {
        uint hash = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        return (hash ^ (hash >> 16)) / (float)uint.MaxValue;
    }

    private readonly struct PaintEntry
    {
        public PaintEntry(
            int prototypeIndex,
            float weight,
            float coverage,
            float noiseScale,
            float noiseInfluence,
            int seed)
        {
            PrototypeIndex = prototypeIndex;
            Weight = weight;
            Coverage = coverage;
            NoiseScale = noiseScale;
            NoiseInfluence = noiseInfluence;
            Seed = seed;
        }

        public int PrototypeIndex { get; }
        public float Weight { get; }
        public float Coverage { get; }
        public float NoiseScale { get; }
        public float NoiseInfluence { get; }
        public int Seed { get; }
    }
}
}
#endif
