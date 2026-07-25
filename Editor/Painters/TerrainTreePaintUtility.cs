#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

internal static class TerrainTreePaintUtility
{
    private const int MaxTreesPerDab = 2048;

    public static bool ValidatePreset(
        TerrainData terrainData,
        TerrainTreePaintPreset preset,
        out string error)
    {
        return TryBuildPaintEntries(terrainData, preset, out _, out error);
    }

    public static void PaintAt(
        Terrain terrain,
        TerrainTreePaintPreset preset,
        Vector3 worldPosition,
        float brushSize,
        float brushStrength,
        float brushFalloff,
        float treesPer100SquareMeters,
        float minimumSpacing,
        bool eraseMode,
        int randomSeed,
        TerrainTreePaintStrokeContext strokeContext)
    {
        if (terrain == null || terrain.terrainData == null || preset == null)
        {
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        if (!TryBuildPaintEntries(terrainData, preset, out List<PaintEntry> entries, out string error))
        {
            throw new InvalidOperationException(error);
        }

        if (entries.Count == 0)
        {
            return;
        }

        float radius = Mathf.Max(0.25f, brushSize) * 0.5f;
        TerrainTreePaintStrokeContext activeContext = strokeContext ?? new TerrainTreePaintStrokeContext();
        TerrainTreePaintStrokeContext.TerrainState state = activeContext.GetState(terrain, minimumSpacing);
        List<TreeInstance> instances = state.Instances;
        System.Random random = new(randomSeed);

        bool changed = eraseMode
            ? Erase(instances, terrain, entries, worldPosition, radius, brushStrength, brushFalloff, random)
            : Paint(
                instances,
                terrain,
                entries,
                worldPosition,
                radius,
                brushStrength,
                brushFalloff,
                treesPer100SquareMeters,
                minimumSpacing,
                state.SpacingIndex,
                random);

        if (!changed)
        {
            return;
        }

        terrainData.treeInstances = instances.ToArray();
        if (eraseMode)
        {
            state.RebuildSpacingIndex(minimumSpacing);
        }

        EditorUtility.SetDirty(terrainData);
        terrain.Flush();
        SceneView.RepaintAll();
    }

    private static bool Paint(
        List<TreeInstance> instances,
        Terrain terrain,
        IReadOnlyList<PaintEntry> entries,
        Vector3 brushCenter,
        float radius,
        float brushStrength,
        float brushFalloff,
        float treesPer100SquareMeters,
        float minimumSpacing,
        TerrainTreeSpacingIndex spacingIndex,
        System.Random random)
    {
        float expectedCandidates = Mathf.PI * radius * radius * Mathf.Max(0f, treesPer100SquareMeters) / 100f;
        int candidateCount = Mathf.FloorToInt(expectedCandidates);
        if (NextFloat(random) < expectedCandidates - candidateCount)
        {
            candidateCount++;
        }

        candidateCount = Mathf.Clamp(candidateCount, 0, MaxTreesPerDab);
        if (candidateCount == 0)
        {
            return false;
        }

        float spacing = Mathf.Max(0f, minimumSpacing);
        float spacingSquared = spacing * spacing;
        bool changed = false;

        for (int i = 0; i < candidateCount; i++)
        {
            Vector2 offset = RandomPointInCircle(random, radius);
            float distance = offset.magnitude;
            float opacity = Mathf.Clamp01(brushStrength) * ComputeBrushFalloff(distance, radius, brushFalloff);
            if (NextFloat(random) > opacity)
            {
                continue;
            }

            Vector3 candidateWorld = brushCenter + new Vector3(offset.x, 0f, offset.y);
            if (!TryWorldToTreePosition(terrain, candidateWorld, out Vector3 normalizedPosition))
            {
                continue;
            }

            Vector3 snappedWorld = TreePositionToWorld(terrain, normalizedPosition);
            Vector2 horizontalPosition = new(snappedWorld.x, snappedWorld.z);
            if (spacingIndex != null && spacingIndex.HasTreeWithin(horizontalPosition, spacingSquared))
            {
                continue;
            }

            PaintEntry entry = ChooseEntry(entries, NextFloat(random));
            instances.Add(CreateTreeInstance(entry, normalizedPosition, random));
            spacingIndex?.Add(horizontalPosition);
            changed = true;
        }

        return changed;
    }

    private static bool Erase(
        List<TreeInstance> instances,
        Terrain terrain,
        IReadOnlyList<PaintEntry> entries,
        Vector3 brushCenter,
        float radius,
        float brushStrength,
        float brushFalloff,
        System.Random random)
    {
        HashSet<int> presetPrototypeIndices = new();
        for (int i = 0; i < entries.Count; i++)
        {
            presetPrototypeIndices.Add(entries[i].PrototypeIndex);
        }

        bool changed = false;
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            TreeInstance instance = instances[i];
            if (!presetPrototypeIndices.Contains(instance.prototypeIndex))
            {
                continue;
            }

            Vector3 treeWorld = TreePositionToWorld(terrain, instance.position);
            float distance = HorizontalDistance(brushCenter, treeWorld);
            if (distance > radius)
            {
                continue;
            }

            float opacity = Mathf.Clamp01(brushStrength) * ComputeBrushFalloff(distance, radius, brushFalloff);
            if (NextFloat(random) > opacity)
            {
                continue;
            }

            instances.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private static bool TryBuildPaintEntries(
        TerrainData terrainData,
        TerrainTreePaintPreset preset,
        out List<PaintEntry> result,
        out string error)
    {
        result = new List<PaintEntry>();
        HashSet<int> usedPrototypeIndices = new();
        float totalWeight = 0f;

        if (preset == null)
        {
            error = "Assign a Tree Paint Preset.";
            return false;
        }

        foreach (TerrainTreePaintPreset.Entry entry in preset.Entries)
        {
            if (!entry.enabled)
            {
                continue;
            }

            if (!TerrainPrototypeResolver.TryResolveTree(
                    terrainData,
                    entry.prefab,
                    out int prototypeIndex,
                    out error))
            {
                return false;
            }

            if (!usedPrototypeIndices.Add(prototypeIndex))
            {
                error = $"Tree prefab '{entry.prefab.name}' is assigned to more than one enabled preset entry.";
                return false;
            }

            if (entry.weight <= 0f)
            {
                continue;
            }

            float weight = Mathf.Max(0f, entry.weight);
            totalWeight += weight;
            result.Add(new PaintEntry(
                prototypeIndex,
                weight,
                entry.randomRotation,
                PositiveOrderedRange(entry.minHeightScale, entry.maxHeightScale),
                entry.lockWidthToHeight,
                PositiveOrderedRange(entry.minWidthScale, entry.maxWidthScale)));
        }

        if (totalWeight <= 0f)
        {
            result.Clear();
            error = "At least one enabled tree entry must have a weight greater than zero.";
            return false;
        }

        float cumulativeWeight = 0f;
        for (int i = 0; i < result.Count; i++)
        {
            cumulativeWeight += result[i].Weight / totalWeight;
            result[i] = result[i].WithCumulativeWeight(i == result.Count - 1 ? 1f : cumulativeWeight);
        }

        error = null;
        return true;
    }

    private static TreeInstance CreateTreeInstance(PaintEntry entry, Vector3 normalizedPosition, System.Random random)
    {
        float heightScale = Mathf.Lerp(entry.HeightScaleRange.x, entry.HeightScaleRange.y, NextFloat(random));
        float widthScale = entry.LockWidthToHeight
            ? heightScale
            : Mathf.Lerp(entry.WidthScaleRange.x, entry.WidthScaleRange.y, NextFloat(random));

        return new TreeInstance
        {
            position = normalizedPosition,
            prototypeIndex = entry.PrototypeIndex,
            widthScale = widthScale,
            heightScale = heightScale,
            rotation = entry.RandomRotation ? NextFloat(random) * Mathf.PI * 2f : 0f,
            color = Color.white,
            lightmapColor = Color.white
        };
    }

    private static PaintEntry ChooseEntry(IReadOnlyList<PaintEntry> entries, float randomValue)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (randomValue <= entries[i].CumulativeWeight)
            {
                return entries[i];
            }
        }

        return entries[entries.Count - 1];
    }

    private static bool TryWorldToTreePosition(Terrain terrain, Vector3 worldPosition, out Vector3 normalizedPosition)
    {
        normalizedPosition = default;
        TerrainData terrainData = terrain.terrainData;
        Vector3 size = terrainData.size;
        if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
        {
            return false;
        }

        Vector3 local = terrain.transform.InverseTransformPoint(worldPosition);
        float normalizedX = local.x / size.x;
        float normalizedZ = local.z / size.z;
        if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
        {
            return false;
        }

        float normalizedY = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) / size.y;
        normalizedPosition = new Vector3(normalizedX, normalizedY, normalizedZ);
        return true;
    }

    private static Vector3 TreePositionToWorld(Terrain terrain, Vector3 normalizedPosition)
    {
        Vector3 size = terrain.terrainData.size;
        return terrain.transform.TransformPoint(new Vector3(
            normalizedPosition.x * size.x,
            normalizedPosition.y * size.y,
            normalizedPosition.z * size.z));
    }

    private static Vector2 RandomPointInCircle(System.Random random, float radius)
    {
        float angle = NextFloat(random) * Mathf.PI * 2f;
        float distance = Mathf.Sqrt(NextFloat(random)) * radius;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
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

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static Vector2 PositiveOrderedRange(float first, float second)
    {
        float minimum = Mathf.Max(0.01f, Mathf.Min(first, second));
        float maximum = Mathf.Max(minimum, Mathf.Max(first, second));
        return new Vector2(minimum, maximum);
    }

    private static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private readonly struct PaintEntry
    {
        public PaintEntry(
            int prototypeIndex,
            float weight,
            bool randomRotation,
            Vector2 heightScaleRange,
            bool lockWidthToHeight,
            Vector2 widthScaleRange,
            float cumulativeWeight = 0f)
        {
            PrototypeIndex = prototypeIndex;
            Weight = weight;
            RandomRotation = randomRotation;
            HeightScaleRange = heightScaleRange;
            LockWidthToHeight = lockWidthToHeight;
            WidthScaleRange = widthScaleRange;
            CumulativeWeight = cumulativeWeight;
        }

        public int PrototypeIndex { get; }
        public float Weight { get; }
        public bool RandomRotation { get; }
        public Vector2 HeightScaleRange { get; }
        public bool LockWidthToHeight { get; }
        public Vector2 WidthScaleRange { get; }
        public float CumulativeWeight { get; }

        public PaintEntry WithCumulativeWeight(float cumulativeWeight)
        {
            return new PaintEntry(
                PrototypeIndex,
                Weight,
                RandomRotation,
                HeightScaleRange,
                LockWidthToHeight,
                WidthScaleRange,
                cumulativeWeight);
        }
    }

}
}
#endif
