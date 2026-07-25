using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dominikozi.TerrainTools.Editor
{
    [InitializeOnLoad]
    internal static class TerrainSurfaceTerrainChangeWatcher
    {
        static TerrainSurfaceTerrainChangeWatcher()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            TerrainCallbacks.textureChanged += OnTextureChanged;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void OnHeightmapChanged(Terrain terrain, RectInt region, bool synched)
        {
            ScheduleRebindIfManaged(terrain);
        }

        private static void OnTextureChanged(Terrain terrain, string textureName, RectInt region, bool synched)
        {
            ScheduleRebindIfManaged(terrain);
        }

        private static void OnSceneSaved(Scene scene)
        {
            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                if (groups[groupIndex].gameObject.scene != scene)
                {
                    continue;
                }

                TerrainSurfaceEditorRebindService.ScheduleRebind();
                return;
            }
        }

        private static void ScheduleRebindIfManaged(Terrain changedTerrain)
        {
            if (changedTerrain == null)
            {
                return;
            }

            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                TerrainSurfaceGroup group = groups[groupIndex];
                for (int terrainIndex = 0; terrainIndex < group.Terrains.Count; terrainIndex++)
                {
                    if (group.Terrains[terrainIndex] != changedTerrain)
                    {
                        continue;
                    }

                    TerrainSurfaceEditorRebindService.ScheduleRebind();
                    return;
                }
            }
        }
    }
}
