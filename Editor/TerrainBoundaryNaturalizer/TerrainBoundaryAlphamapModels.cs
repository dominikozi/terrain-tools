using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal sealed class TerrainBoundaryTileSnapshot
    {
        private const float CoordinateEpsilon = 0.001f;

        public Terrain Terrain { get; }
        public TerrainData TerrainData { get; }
        public TerrainBoundaryGridRect Rect { get; }
        public float[,,] Weights { get; }
        public int LayerCount => Weights.GetLength(2);
        public int Width => Rect.Width;
        public int Height => Rect.Height;
        public int AlphamapWidth => TerrainData.alphamapWidth;
        public int AlphamapHeight => TerrainData.alphamapHeight;
        public Vector2 MetersPerTexel { get; }
        public float MaximumMetersPerTexel => Mathf.Max(MetersPerTexel.x, MetersPerTexel.y);

        public TerrainBoundaryTileSnapshot(
            Terrain terrain,
            TerrainBoundaryGridRect rect,
            float[,,] weights)
        {
            Terrain = terrain != null ? terrain : throw new ArgumentNullException(nameof(terrain));
            TerrainData = terrain.terrainData != null
                ? terrain.terrainData
                : throw new ArgumentException("Terrain has no TerrainData.", nameof(terrain));
            Rect = rect;
            Weights = weights ?? throw new ArgumentNullException(nameof(weights));
            if (weights.GetLength(0) != rect.Height || weights.GetLength(1) != rect.Width)
            {
                throw new ArgumentException("Snapshot dimensions do not match its alphamap rectangle.", nameof(weights));
            }

            MetersPerTexel = new Vector2(
                TerrainData.size.x / Mathf.Max(1, AlphamapWidth - 1),
                TerrainData.size.z / Mathf.Max(1, AlphamapHeight - 1));
        }

        public Vector3 LocalGridToWorld(int localX, int localY)
        {
            return TerrainBoundaryTerrainUtility.GridToWorld(
                Terrain,
                Rect.X + localX,
                Rect.Y + localY,
                AlphamapWidth,
                AlphamapHeight);
        }

        public void CopyWeights(int localX, int localY, float[] destination)
        {
            int count = Mathf.Min(destination.Length, LayerCount);
            for (int layer = 0; layer < count; layer++)
            {
                destination[layer] = Weights[localY, localX, layer];
            }
        }

        public bool TrySample(Vector2 worldPosition, float[] destination)
        {
            Vector3 local = Terrain.transform.InverseTransformPoint(new Vector3(
                worldPosition.x,
                Terrain.transform.position.y,
                worldPosition.y));
            Vector3 size = TerrainData.size;
            if (size.x <= 0f || size.z <= 0f)
            {
                return false;
            }

            float normalizedX = local.x / size.x;
            float normalizedY = local.z / size.z;
            if (normalizedX < -CoordinateEpsilon || normalizedX > 1f + CoordinateEpsilon
                || normalizedY < -CoordinateEpsilon || normalizedY > 1f + CoordinateEpsilon)
            {
                return false;
            }

            float gridX = Mathf.Clamp01(normalizedX) * Mathf.Max(1, AlphamapWidth - 1);
            float gridY = Mathf.Clamp01(normalizedY) * Mathf.Max(1, AlphamapHeight - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(gridX), 0, AlphamapWidth - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(gridY), 0, AlphamapHeight - 1);
            int x1 = Mathf.Min(x0 + 1, AlphamapWidth - 1);
            int y1 = Mathf.Min(y0 + 1, AlphamapHeight - 1);
            if (!ContainsGlobalGrid(x0, y0) || !ContainsGlobalGrid(x1, y1))
            {
                return false;
            }

            float tx = gridX - x0;
            float ty = gridY - y0;
            int localX0 = x0 - Rect.X;
            int localY0 = y0 - Rect.Y;
            int localX1 = x1 - Rect.X;
            int localY1 = y1 - Rect.Y;
            int count = Mathf.Min(destination.Length, LayerCount);
            float total = 0f;
            for (int layer = 0; layer < count; layer++)
            {
                float lower = Mathf.Lerp(
                    Weights[localY0, localX0, layer],
                    Weights[localY0, localX1, layer],
                    tx);
                float upper = Mathf.Lerp(
                    Weights[localY1, localX0, layer],
                    Weights[localY1, localX1, layer],
                    tx);
                float value = Mathf.Max(0f, Mathf.Lerp(lower, upper, ty));
                destination[layer] = value;
                total += value;
            }

            if (total <= 0.000001f)
            {
                return false;
            }

            for (int layer = 0; layer < count; layer++)
            {
                destination[layer] /= total;
            }

            return true;
        }

        private bool ContainsGlobalGrid(int x, int y)
        {
            return x >= Rect.X && x < Rect.X + Rect.Width
                && y >= Rect.Y && y < Rect.Y + Rect.Height;
        }
    }

    internal sealed class TerrainBoundaryWorldSampler
    {
        private readonly IReadOnlyList<TerrainBoundaryTileSnapshot> snapshots;

        public TerrainBoundaryWorldSampler(IReadOnlyList<TerrainBoundaryTileSnapshot> sourceSnapshots)
        {
            snapshots = sourceSnapshots ?? throw new ArgumentNullException(nameof(sourceSnapshots));
        }

        public bool TrySample(Vector2 worldPosition, float[] destination)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].TrySample(worldPosition, destination))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class TerrainBoundaryTileResult
    {
        public Terrain Terrain { get; }
        public int X { get; }
        public int Y { get; }
        public float[,,] Weights { get; }
        public int ChangedTexelCount { get; }
        public int Width => Weights.GetLength(1);
        public int Height => Weights.GetLength(0);

        public TerrainBoundaryTileResult(
            Terrain terrain,
            int x,
            int y,
            float[,,] weights,
            int changedTexelCount)
        {
            Terrain = terrain;
            X = x;
            Y = y;
            Weights = weights;
            ChangedTexelCount = changedTexelCount;
        }
    }
}
