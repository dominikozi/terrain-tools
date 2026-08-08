using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal readonly struct TerrainBoundaryGridRect
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public TerrainBoundaryGridRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    internal static class TerrainBoundaryTerrainUtility
    {
        public static Bounds GetTerrainBounds(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            Vector3 center = terrain.transform.TransformPoint(data.size * 0.5f);
            Vector3 scaledSize = Vector3.Scale(data.size, terrain.transform.lossyScale);
            return new Bounds(center, new Vector3(
                Mathf.Abs(scaledSize.x),
                Mathf.Abs(scaledSize.y),
                Mathf.Abs(scaledSize.z)));
        }

        public static TerrainBoundaryGridRect GetGridRect(
            Terrain terrain,
            Bounds worldBounds,
            int resolutionX,
            int resolutionY,
            int padding = 1)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float minNormalizedX = Mathf.Clamp01((worldBounds.min.x - terrainPosition.x) / size.x);
            float maxNormalizedX = Mathf.Clamp01((worldBounds.max.x - terrainPosition.x) / size.x);
            float minNormalizedZ = Mathf.Clamp01((worldBounds.min.z - terrainPosition.z) / size.z);
            float maxNormalizedZ = Mathf.Clamp01((worldBounds.max.z - terrainPosition.z) / size.z);

            int minX = Mathf.Clamp(
                Mathf.FloorToInt(minNormalizedX * (resolutionX - 1)) - padding,
                0,
                resolutionX - 1);
            int maxX = Mathf.Clamp(
                Mathf.CeilToInt(maxNormalizedX * (resolutionX - 1)) + padding,
                0,
                resolutionX - 1);
            int minY = Mathf.Clamp(
                Mathf.FloorToInt(minNormalizedZ * (resolutionY - 1)) - padding,
                0,
                resolutionY - 1);
            int maxY = Mathf.Clamp(
                Mathf.CeilToInt(maxNormalizedZ * (resolutionY - 1)) + padding,
                0,
                resolutionY - 1);
            return new TerrainBoundaryGridRect(
                minX,
                minY,
                Mathf.Max(1, maxX - minX + 1),
                Mathf.Max(1, maxY - minY + 1));
        }

        public static Vector3 GridToWorld(
            Terrain terrain,
            int gridX,
            int gridY,
            int resolutionX,
            int resolutionY)
        {
            TerrainData data = terrain.terrainData;
            float normalizedX = resolutionX > 1 ? gridX / (float)(resolutionX - 1) : 0f;
            float normalizedZ = resolutionY > 1 ? gridY / (float)(resolutionY - 1) : 0f;
            return terrain.transform.TransformPoint(new Vector3(
                normalizedX * data.size.x,
                0f,
                normalizedZ * data.size.z));
        }

        public static float SampleSignedDomainWarpedNoise(
            Vector3 worldPosition,
            float scale,
            int seed,
            int channel)
        {
            float safeScale = Mathf.Max(0.001f, scale);
            float warpX = SampleNoise(worldPosition, safeScale * 0.73f, seed + 101, channel + 3) - 0.5f;
            float warpZ = SampleNoise(worldPosition, safeScale * 0.81f, seed - 79, channel + 7) - 0.5f;
            Vector3 warped = worldPosition + new Vector3(warpX, 0f, warpZ) * safeScale * 0.35f;
            return SampleNoise(warped, safeScale, seed, channel) * 2f - 1f;
        }

        private static float SampleNoise(Vector3 worldPosition, float scale, int seed, int channel)
        {
            float safeScale = Mathf.Max(0.001f, scale);
            float offsetX = seed * 0.01373f + channel * 113.17f + 37.11f;
            float offsetZ = seed * 0.02191f - channel * 71.93f - 19.73f;
            return Mathf.PerlinNoise(
                worldPosition.x / safeScale + offsetX,
                worldPosition.z / safeScale + offsetZ);
        }
    }
}
