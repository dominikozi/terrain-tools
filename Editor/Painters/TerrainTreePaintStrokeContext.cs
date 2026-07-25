#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal sealed class TerrainTreePaintStrokeContext
{
    private readonly Dictionary<TerrainData, TerrainState> states = new();

    public TerrainState GetState(Terrain terrain, float minimumSpacing)
    {
        TerrainData terrainData = terrain.terrainData;
        if (!states.TryGetValue(terrainData, out TerrainState state))
        {
            state = new TerrainState(terrain);
            states.Add(terrainData, state);
        }

        state.EnsureSpacingIndex(minimumSpacing);
        return state;
    }

    internal sealed class TerrainState
    {
        private readonly Terrain terrain;
        private float indexedSpacing = -1f;

        public TerrainState(Terrain terrain)
        {
            this.terrain = terrain;
            Instances = new List<TreeInstance>(terrain.terrainData.treeInstances);
        }

        public List<TreeInstance> Instances { get; }
        public TerrainTreeSpacingIndex SpacingIndex { get; private set; }

        public void EnsureSpacingIndex(float spacing)
        {
            float clampedSpacing = Mathf.Max(0f, spacing);
            if (Mathf.Approximately(clampedSpacing, indexedSpacing))
            {
                return;
            }

            indexedSpacing = clampedSpacing;
            SpacingIndex = clampedSpacing > 0f
                ? new TerrainTreeSpacingIndex(clampedSpacing, terrain, Instances)
                : null;
        }

        public void RebuildSpacingIndex(float spacing)
        {
            indexedSpacing = -1f;
            EnsureSpacingIndex(spacing);
        }
    }
}

internal sealed class TerrainTreeSpacingIndex
{
    private readonly float cellSize;
    private readonly Dictionary<Vector2Int, List<Vector2>> cells = new();

    public TerrainTreeSpacingIndex(float cellSize, Terrain terrain, IReadOnlyList<TreeInstance> instances)
    {
        this.cellSize = Mathf.Max(0.01f, cellSize);
        Vector3 size = terrain.terrainData.size;
        Transform transform = terrain.transform;
        for (int i = 0; i < instances.Count; i++)
        {
            Vector3 normalized = instances[i].position;
            Vector3 world = transform.TransformPoint(new Vector3(
                normalized.x * size.x,
                normalized.y * size.y,
                normalized.z * size.z));
            Add(new Vector2(world.x, world.z));
        }
    }

    public void Add(Vector2 position)
    {
        Vector2Int cell = GetCell(position);
        if (!cells.TryGetValue(cell, out List<Vector2> positions))
        {
            positions = new List<Vector2>();
            cells.Add(cell, positions);
        }

        positions.Add(position);
    }

    public bool HasTreeWithin(Vector2 candidate, float spacingSquared)
    {
        Vector2Int center = GetCell(candidate);
        for (int y = center.y - 1; y <= center.y + 1; y++)
        {
            for (int x = center.x - 1; x <= center.x + 1; x++)
            {
                if (!cells.TryGetValue(new Vector2Int(x, y), out List<Vector2> positions))
                {
                    continue;
                }

                for (int i = 0; i < positions.Count; i++)
                {
                    if ((positions[i] - candidate).sqrMagnitude < spacingSquared)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private Vector2Int GetCell(Vector2 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize));
    }
}
}
#endif
