using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainSurfaceSetupMenu
    {
        [MenuItem("Tools/Terrain Tools/Terrain Surface/Create Group From Selected Terrains")]
        private static void CreateGroupFromSelection()
        {
            List<Terrain> terrains = GetSelectedTerrains();
            if (terrains.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Create Terrain Surface Group",
                    "Select one or more Terrain objects, or a parent containing Terrain children.",
                    "OK");
                return;
            }

            GameObject groupObject = new GameObject("Terrain Surface Group");
            Undo.RegisterCreatedObjectUndo(groupObject, "Create Terrain Surface Group");
            TerrainSurfaceGroup group = Undo.AddComponent<TerrainSurfaceGroup>(groupObject);
            group.SetTerrains(terrains);
            Selection.activeGameObject = groupObject;
            EditorGUIUtility.PingObject(groupObject);
        }

        [MenuItem("Tools/Terrain Tools/Terrain Surface/Create Group From Selected Terrains", true)]
        private static bool ValidateCreateGroupFromSelection()
        {
            return Selection.gameObjects.Length > 0;
        }

        private static List<Terrain> GetSelectedTerrains()
        {
            List<Terrain> result = new List<Terrain>();
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                Terrain direct = selected[i].GetComponent<Terrain>();
                if (direct != null && !result.Contains(direct))
                {
                    result.Add(direct);
                }

                Terrain[] children = selected[i].GetComponentsInChildren<Terrain>(includeInactive: true);
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    if (!result.Contains(children[childIndex]))
                    {
                        result.Add(children[childIndex]);
                    }
                }
            }
            return result;
        }
    }
}
