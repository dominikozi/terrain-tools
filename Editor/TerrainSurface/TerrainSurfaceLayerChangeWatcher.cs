using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [InitializeOnLoad]
    internal static class TerrainSurfaceLayerChangeWatcher
    {
        static TerrainSurfaceLayerChangeWatcher()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int eventIndex = 0; eventIndex < stream.length; eventIndex++)
            {
                if (stream.GetEventType(eventIndex) != ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    continue;
                }

                stream.GetChangeAssetObjectPropertiesEvent(
                    eventIndex,
                    out ChangeAssetObjectPropertiesEventArgs change);
                TerrainLayer changedLayer = ResolveTerrainLayer(change);
                if (changedLayer == null || !IsUsedByLoadedTerrainSurfaceGroup(changedLayer))
                {
                    continue;
                }

                TerrainSurfaceEditorRebindService.ScheduleRebind();
                return;
            }
        }

        private static TerrainLayer ResolveTerrainLayer(ChangeAssetObjectPropertiesEventArgs change)
        {
            if (EditorUtility.EntityIdToObject(change.entityId) is TerrainLayer loadedLayer)
            {
                return loadedLayer;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(change.guid.ToString());
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
        }

        private static bool IsUsedByLoadedTerrainSurfaceGroup(TerrainLayer layer)
        {
            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                TerrainSurfaceProfile profile = groups[groupIndex].Profile;
                if (profile == null)
                {
                    continue;
                }

                IReadOnlyList<TerrainSurfaceLayerSettings> profileLayers = profile.Layers;
                for (int layerIndex = 0; layerIndex < profileLayers.Count; layerIndex++)
                {
                    if (profileLayers[layerIndex]?.TerrainLayer == layer)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
