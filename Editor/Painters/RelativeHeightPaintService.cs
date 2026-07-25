#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal static class RelativeHeightPaintService
{
    public static bool PaintTerrain(
        Terrain terrain,
        Vector3 brushCenter,
        Vector3 fieldPatternOrigin,
        float referenceWorldY,
        float brushExtent,
        Vector3 slopeDirection,
        Vector3 fieldDirection,
        RelativeHeightBrushSettings settings,
        RelativeHeightBrushEvaluator evaluator,
        Action<Terrain> registerUndo)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        if (resolution <= 1 ||
            terrainData.size.x <= 0f ||
            terrainData.size.y <= 0f ||
            terrainData.size.z <= 0f)
        {
            return false;
        }

        Vector3 localCenter = terrain.transform.InverseTransformPoint(brushCenter);
        float centerX = localCenter.x / terrainData.size.x * (resolution - 1);
        float centerZ = localCenter.z / terrainData.size.z * (resolution - 1);
        Vector3 worldSize = Vector3.Scale(terrainData.size, terrain.transform.lossyScale);
        float worldWidth = Mathf.Max(0.001f, Mathf.Abs(worldSize.x));
        float worldLength = Mathf.Max(0.001f, Mathf.Abs(worldSize.z));
        int extentPixelsX = Mathf.CeilToInt(brushExtent / worldWidth * (resolution - 1)) + 1;
        int extentPixelsZ = Mathf.CeilToInt(brushExtent / worldLength * (resolution - 1)) + 1;

        int unclampedMinX = Mathf.FloorToInt(centerX) - extentPixelsX;
        int unclampedMaxX = Mathf.CeilToInt(centerX) + extentPixelsX;
        int unclampedMinZ = Mathf.FloorToInt(centerZ) - extentPixelsZ;
        int unclampedMaxZ = Mathf.CeilToInt(centerZ) + extentPixelsZ;
        if (unclampedMaxX < 0 ||
            unclampedMinX > resolution - 1 ||
            unclampedMaxZ < 0 ||
            unclampedMinZ > resolution - 1)
        {
            return false;
        }

        int minX = Mathf.Clamp(unclampedMinX, 0, resolution - 1);
        int maxX = Mathf.Clamp(unclampedMaxX, 0, resolution - 1);
        int minZ = Mathf.Clamp(unclampedMinZ, 0, resolution - 1);
        int maxZ = Mathf.Clamp(unclampedMaxZ, 0, resolution - 1);
        int width = maxX - minX + 1;
        int height = maxZ - minZ + 1;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        float[,] heights = terrainData.GetHeights(minX, minZ, width, height);
        Vector3 slopeRight = new(slopeDirection.z, 0f, -slopeDirection.x);
        Vector3 fieldRight = new(fieldDirection.z, 0f, -fieldDirection.x);
        bool usesFurrowSampling =
            settings.Shape == HeightBrushShape.FieldFurrows ||
            settings.Shape == HeightBrushShape.SingleFurrow;
        float furrowSampleHalfSpanAlong = usesFurrowSampling
            ? ComputeHeightmapSampleSpacingAlongDirection(terrain, fieldDirection) * 0.5f
            : 0f;
        float furrowSampleHalfSpanRight = usesFurrowSampling
            ? ComputeHeightmapSampleSpacingAlongDirection(terrain, fieldRight) * 0.5f
            : 0f;
        bool changed = false;

        for (int localZ = 0; localZ < height; localZ++)
        {
            int mapZ = minZ + localZ;
            for (int localX = 0; localX < width; localX++)
            {
                int mapX = minX + localX;
                Vector3 sampleWorld = HeightmapCoordToWorld(terrain, mapX, mapZ);
                if (!evaluator.TryGetSample(
                        brushCenter,
                        fieldPatternOrigin,
                        sampleWorld,
                        slopeDirection,
                        slopeRight,
                        fieldDirection,
                        fieldRight,
                        furrowSampleHalfSpanAlong,
                        furrowSampleHalfSpanRight,
                        out float targetOffset,
                        out float opacity,
                        out bool useSampleHeightAsBase) ||
                    opacity <= 0f)
                {
                    continue;
                }

                float currentHeight = heights[localZ, localX];
                float baseWorldY = useSampleHeightAsBase
                    ? NormalizedHeightToWorldY(terrain, currentHeight)
                    : referenceWorldY;
                float targetHeight = WorldYToNormalizedHeight(terrain, baseWorldY + targetOffset);
                float newHeight = Mathf.Lerp(currentHeight, targetHeight, opacity);
                if (Mathf.Approximately(currentHeight, newHeight))
                {
                    continue;
                }

                heights[localZ, localX] = newHeight;
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        registerUndo(terrain);
        terrainData.SetHeightsDelayLOD(minX, minZ, heights);
        EditorUtility.SetDirty(terrainData);
        SceneView.RepaintAll();
        return true;
    }

    internal static float ComputeHeightmapSampleSpacingAlongDirection(
        Terrain terrain,
        Vector3 direction)
    {
        TerrainData terrainData = terrain != null ? terrain.terrainData : null;
        if (terrainData == null || terrainData.heightmapResolution <= 1)
        {
            return 0f;
        }

        Vector3 worldSize = Vector3.Scale(terrainData.size, terrain.transform.lossyScale);
        float cellX = Mathf.Abs(worldSize.x) / (terrainData.heightmapResolution - 1);
        float cellZ = Mathf.Abs(worldSize.z) / (terrainData.heightmapResolution - 1);
        Vector3 normalizedDirection = direction;
        normalizedDirection.y = 0f;
        if (normalizedDirection.sqrMagnitude <= 0f)
        {
            return Mathf.Max(cellX, cellZ);
        }

        normalizedDirection.Normalize();
        return Mathf.Max(
            Mathf.Abs(normalizedDirection.x) * cellX,
            Mathf.Abs(normalizedDirection.z) * cellZ);
    }

    private static Vector3 HeightmapCoordToWorld(Terrain terrain, int heightmapX, int heightmapZ)
    {
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float normalizedX = heightmapX / (float)(resolution - 1);
        float normalizedZ = heightmapZ / (float)(resolution - 1);
        return terrain.transform.TransformPoint(new Vector3(
            normalizedX * terrainData.size.x,
            0f,
            normalizedZ * terrainData.size.z));
    }

    private static float WorldYToNormalizedHeight(Terrain terrain, float worldY)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 localPosition = terrain.transform.InverseTransformPoint(
            new Vector3(terrain.transform.position.x, worldY, terrain.transform.position.z));
        return Mathf.Clamp01(localPosition.y / terrainData.size.y);
    }

    private static float NormalizedHeightToWorldY(Terrain terrain, float normalizedHeight)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 localPosition = new(
            0f,
            Mathf.Clamp01(normalizedHeight) * terrainData.size.y,
            0f);
        return terrain.transform.TransformPoint(localPosition).y;
    }
}
}
#endif
