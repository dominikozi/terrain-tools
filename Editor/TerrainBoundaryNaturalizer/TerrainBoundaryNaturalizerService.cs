using System;
using System.Collections.Generic;
using System.Linq;
using Dominikozi.TerrainTools;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal readonly struct TerrainBoundaryNaturalizationSummary
    {
        public int TilesChanged { get; }
        public int TexelsChanged { get; }

        public TerrainBoundaryNaturalizationSummary(int tilesChanged, int texelsChanged)
        {
            TilesChanged = tilesChanged;
            TexelsChanged = texelsChanged;
        }
    }

    internal static class TerrainBoundaryNaturalizerService
    {
        private const string UndoName = "Naturalize Terrain Boundaries";

        public static bool TryValidate(
            TerrainSurfaceGroup group,
            TerrainBoundaryNaturalizerSettings settings,
            out List<Terrain> terrains,
            out int layerAIndex,
            out int layerBIndex,
            out string error)
        {
            terrains = new List<Terrain>();
            layerAIndex = -1;
            layerBIndex = -1;
            if (group == null)
            {
                error = "Assign a Terrain Surface Group.";
                return false;
            }

            if (settings == null)
            {
                error = "Naturalizer settings are missing.";
                return false;
            }

            settings.Sanitize();
            for (int i = 0; i < group.Terrains.Count; i++)
            {
                Terrain terrain = group.Terrains[i];
                if (terrain != null && terrain.terrainData != null && !terrains.Contains(terrain))
                {
                    terrains.Add(terrain);
                }
            }

            if (terrains.Count == 0)
            {
                error = "The Terrain Surface Group contains no valid Terrains.";
                return false;
            }

            terrains.Sort((first, second) =>
            {
                int z = first.transform.position.z.CompareTo(second.transform.position.z);
                return z != 0 ? z : first.transform.position.x.CompareTo(second.transform.position.x);
            });

            TerrainLayer[] canonicalLayers = terrains[0].terrainData.terrainLayers;
            if (canonicalLayers == null || canonicalLayers.Length < 2)
            {
                error = "Terrains need at least two TerrainLayers.";
                return false;
            }

            for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
            {
                TerrainData data = terrains[terrainIndex].terrainData;
                if (data.alphamapWidth <= 0 || data.alphamapHeight <= 0
                    || data.alphamapLayers != canonicalLayers.Length)
                {
                    error = $"Terrain '{terrains[terrainIndex].name}' has an incompatible alphamap.";
                    return false;
                }

                TerrainLayer[] layers = data.terrainLayers;
                if (layers.Length != canonicalLayers.Length)
                {
                    error = $"Terrain '{terrains[terrainIndex].name}' has a different TerrainLayer count.";
                    return false;
                }

                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    if (layers[layerIndex] != canonicalLayers[layerIndex])
                    {
                        error =
                            $"Terrain '{terrains[terrainIndex].name}' has a different TerrainLayer order at index {layerIndex}.";
                        return false;
                    }
                }
            }

            if (settings.LayerScope == TerrainBoundaryLayerScope.SelectedPair)
            {
                layerAIndex = Array.IndexOf(canonicalLayers, settings.LayerA);
                layerBIndex = Array.IndexOf(canonicalLayers, settings.LayerB);
                if (layerAIndex < 0 || layerBIndex < 0)
                {
                    error = "Layer A and Layer B must belong to the Terrain Surface Group.";
                    return false;
                }

                if (layerAIndex == layerBIndex)
                {
                    error = "Layer A and Layer B must be different.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static TerrainBoundaryNaturalizationSummary Naturalize(
            TerrainSurfaceGroup group,
            TerrainBoundaryStroke stroke,
            TerrainBoundaryNaturalizerSettings settings)
        {
            if (stroke == null || stroke.PointCount == 0)
            {
                return default;
            }

            if (!TryValidate(
                    group,
                    settings,
                    out List<Terrain> terrains,
                    out int layerAIndex,
                    out int layerBIndex,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            float maximumTexelSize = 0f;
            for (int i = 0; i < terrains.Count; i++)
            {
                TerrainData data = terrains[i].terrainData;
                maximumTexelSize = Mathf.Max(
                    maximumTexelSize,
                    data.size.x / Mathf.Max(1, data.alphamapWidth - 1),
                    data.size.z / Mathf.Max(1, data.alphamapHeight - 1));
            }

            Bounds effectBounds = stroke.GetWorldBounds(settings.BrushRadius);
            float sourcePadding = settings.MaximumDisplacement + maximumTexelSize * 4f;
            if (settings.Character == TerrainBoundaryCharacter.Islands)
            {
                sourcePadding += settings.IslandReach + settings.IslandSize;
            }
            Bounds sourceBounds = ExpandXZ(effectBounds, sourcePadding);

            List<TerrainBoundaryTileSnapshot> snapshots = new();
            HashSet<Terrain> outputTerrains = new();
            try
            {
                for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
                {
                    Terrain terrain = terrains[terrainIndex];
                    Bounds terrainBounds = TerrainBoundaryTerrainUtility.GetTerrainBounds(terrain);
                    if (!IntersectsXZ(sourceBounds, terrainBounds))
                    {
                        continue;
                    }

                    TerrainData data = terrain.terrainData;
                    TerrainBoundaryGridRect rect = TerrainBoundaryTerrainUtility.GetGridRect(
                        terrain,
                        sourceBounds,
                        data.alphamapWidth,
                        data.alphamapHeight,
                        2);
                    float[,,] weights = data.GetAlphamaps(rect.X, rect.Y, rect.Width, rect.Height);
                    snapshots.Add(new TerrainBoundaryTileSnapshot(terrain, rect, weights));
                    if (IntersectsXZ(effectBounds, terrainBounds))
                    {
                        outputTerrains.Add(terrain);
                    }
                }

                TerrainBoundaryWorldSampler sampler = new(snapshots);
                List<TerrainBoundaryTileSnapshot> outputSnapshots = snapshots
                    .Where(snapshot => outputTerrains.Contains(snapshot.Terrain))
                    .ToList();
                List<TerrainBoundaryTileResult> results = new();
                for (int tileIndex = 0; tileIndex < outputSnapshots.Count; tileIndex++)
                {
                    TerrainBoundaryTileSnapshot snapshot = outputSnapshots[tileIndex];
                    int capturedIndex = tileIndex;
                    TerrainBoundaryTileResult result = TerrainBoundaryNaturalizerProcessor.Process(
                        snapshot,
                        sampler,
                        stroke,
                        settings,
                        layerAIndex,
                        layerBIndex,
                        localProgress => EditorUtility.DisplayCancelableProgressBar(
                            "Naturalizing Terrain Boundaries",
                            $"{snapshot.Terrain.name} ({capturedIndex + 1}/{outputSnapshots.Count})",
                            (capturedIndex + localProgress) / Mathf.Max(1f, outputSnapshots.Count)));
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Naturalizing Terrain Boundaries",
                        "Preparing TerrainData changes",
                        0.98f))
                {
                    throw new OperationCanceledException("Terrain boundary naturalization was cancelled.");
                }

                return ApplyResults(results);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static TerrainSurfaceGroup FindGroupForSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            TerrainSurfaceGroup selectedGroup = selected.GetComponentInParent<TerrainSurfaceGroup>();
            if (selectedGroup != null)
            {
                return selectedGroup;
            }

            Terrain selectedTerrain = selected.GetComponentInParent<Terrain>();
            if (selectedTerrain == null)
            {
                return null;
            }

            TerrainSurfaceGroup[] groups = UnityEngine.Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Exclude);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                for (int terrainIndex = 0; terrainIndex < groups[groupIndex].Terrains.Count; terrainIndex++)
                {
                    if (groups[groupIndex].Terrains[terrainIndex] == selectedTerrain)
                    {
                        return groups[groupIndex];
                    }
                }
            }

            return null;
        }

        public static bool TryRaycast(
            TerrainSurfaceGroup group,
            Ray ray,
            out Vector3 point,
            out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            if (group == null)
            {
                return false;
            }

            bool found = false;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < group.Terrains.Count; i++)
            {
                Terrain terrain = group.Terrains[i];
                TerrainCollider collider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
                if (collider == null || !collider.enabled
                    || !collider.Raycast(ray, out RaycastHit hit, float.MaxValue)
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                found = true;
                closestDistance = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }

            return found;
        }

        internal static TerrainBoundaryNaturalizationSummary ApplyResults(
            IReadOnlyList<TerrainBoundaryTileResult> results)
        {
            if (results.Count == 0)
            {
                return default;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                HashSet<UnityEngine.Object> registered = new();
                List<UnityEngine.Object> undoObjects = new();
                for (int i = 0; i < results.Count; i++)
                {
                    TerrainData data = results[i].Terrain.terrainData;
                    if (registered.Add(data))
                    {
                        undoObjects.Add(data);
                    }

                    Texture2D[] alphamapTextures = data.alphamapTextures;
                    for (int textureIndex = 0; textureIndex < alphamapTextures.Length; textureIndex++)
                    {
                        Texture2D texture = alphamapTextures[textureIndex];
                        if (texture != null && registered.Add(texture))
                        {
                            undoObjects.Add(texture);
                        }
                    }
                }

                Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), UndoName);

                int texelCount = 0;
                for (int i = 0; i < results.Count; i++)
                {
                    TerrainBoundaryTileResult result = results[i];
                    TerrainData data = result.Terrain.terrainData;
                    data.SetAlphamaps(result.X, result.Y, result.Weights);
                    EditorUtility.SetDirty(data);
                    result.Terrain.Flush();
                    texelCount += result.ChangedTexelCount;
                }

                Undo.CollapseUndoOperations(undoGroup);
                SceneView.RepaintAll();
                return new TerrainBoundaryNaturalizationSummary(results.Count, texelCount);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static Bounds ExpandXZ(Bounds bounds, float expansion)
        {
            Vector3 size = bounds.size;
            size.x += Mathf.Max(0f, expansion) * 2f;
            size.z += Mathf.Max(0f, expansion) * 2f;
            bounds.size = size;
            return bounds;
        }

        private static bool IntersectsXZ(Bounds first, Bounds second)
        {
            return first.min.x <= second.max.x
                && first.max.x >= second.min.x
                && first.min.z <= second.max.z
                && first.max.z >= second.min.z;
        }
    }
}
