using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [InitializeOnLoad]
    internal static class TerrainSurfaceEditorRebindService
    {
        private const double RebindDelaySeconds = 0.15;
        private const int RequiredStableEditorFrames = 2;

        private static bool rebindScheduled;
        private static double earliestRebindTime;
        private static int stableEditorFrames;

        static TerrainSurfaceEditorRebindService()
        {
            AssemblyReloadEvents.afterAssemblyReload += ScheduleRebind;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ScheduleRebind();
        }

        internal static void ScheduleRebind()
        {
            earliestRebindTime = EditorApplication.timeSinceStartup + RebindDelaySeconds;
            stableEditorFrames = 0;
            if (rebindScheduled)
            {
                return;
            }

            rebindScheduled = true;
            EditorApplication.update += TryPerformScheduledRebind;
        }

        [MenuItem("Tools/Terrain Tools/Terrain Surface/Rebind Loaded Terrains", priority = 120)]
        private static void RebindFromMenu()
        {
            CancelScheduledRebind();
            RebindLoadedObjects(logResult: true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                ScheduleRebind();
            }
        }

        private static void TryPerformScheduledRebind()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                ShaderUtil.anythingCompiling ||
                EditorApplication.timeSinceStartup < earliestRebindTime)
            {
                stableEditorFrames = 0;
                return;
            }

            stableEditorFrames++;
            if (stableEditorFrames < RequiredStableEditorFrames)
            {
                return;
            }

            CancelScheduledRebind();
            RebindLoadedObjects(logResult: false);
        }

        private static void CancelScheduledRebind()
        {
            EditorApplication.update -= TryPerformScheduledRebind;
            rebindScheduled = false;
            stableEditorFrames = 0;
        }

        private static void RebindLoadedObjects(bool logResult)
        {
            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            int reboundTerrainCount = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                TerrainSurfaceGroup group = groups[i];
                group.Synchronize();
                for (int terrainIndex = 0; terrainIndex < group.Terrains.Count; terrainIndex++)
                {
                    Terrain terrain = group.Terrains[terrainIndex];
                    if (terrain != null)
                    {
                        reboundTerrainCount++;
                    }
                }
            }

            TerrainSurfaceMeshBlend[] meshBlends = Object.FindObjectsByType<TerrainSurfaceMeshBlend>(
                FindObjectsInactive.Include);
            for (int i = 0; i < meshBlends.Length; i++)
            {
                meshBlends[i].Synchronize();
            }

            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();

            if (logResult)
            {
                string message = groups.Length > 0
                    ? $"Terrain Surface: rebuilt {groups.Length} loaded group(s) and {reboundTerrainCount} Terrain renderer(s)."
                    : "Terrain Surface: no loaded TerrainSurfaceGroup was found.";
                Debug.Log(message);
            }
        }
    }
}
