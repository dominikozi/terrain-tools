using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Dominikozi.TerrainTools.Editor
{
    internal sealed class TerrainSurfaceAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            ScheduleDefaultProfileAssets(importedAssets);

            if (didDomainReload ||
                ContainsRelevantAsset(importedAssets) ||
                ContainsRelevantAsset(deletedAssets) ||
                ContainsRelevantAsset(movedAssets) ||
                ContainsRelevantAsset(movedFromAssetPaths))
            {
                TerrainSurfaceEditorRebindService.ScheduleRebind();
            }
        }

        private static void ScheduleDefaultProfileAssets(string[] importedAssets)
        {
            List<string> profilePaths = new List<string>();
            for (int i = 0; i < importedAssets.Length; i++)
            {
                if (Path.GetExtension(importedAssets[i]).Equals(".asset", StringComparison.OrdinalIgnoreCase) &&
                    !importedAssets[i].StartsWith(TerrainToolsPaths.PackageRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    profilePaths.Add(importedAssets[i]);
                }
            }

            if (profilePaths.Count == 0)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                for (int i = 0; i < profilePaths.Count; i++)
                {
                    TerrainSurfaceProfile profile =
                        AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(profilePaths[i]);
                    if (profile == null)
                    {
                        continue;
                    }

                    TerrainToolsAssetLocator.AssignDefaultProfileAssets(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
            };
        }

        private static bool ContainsRelevantAsset(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (path.StartsWith(
                        TerrainToolsPaths.PackageRoot + "/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string extension = Path.GetExtension(path);
                if (extension.Equals(".terrainlayer", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".shader", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".hlsl", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".shadergraph", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
