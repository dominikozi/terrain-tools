using System;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal sealed class TerrainSurfaceProceduralBakerWindow : EditorWindow
    {
        private TerrainSurfaceGroup group;
        private TerrainSurfaceProceduralProfile proceduralProfile;
        private Terrain previewTerrain;
        private TerrainSurfaceAlphamapBackup backupToRestore;
        private int previewResolution = 256;
        private Texture2D previewTexture;
        private Vector2 scroll;
        private string status;
        private MessageType statusType;

        [MenuItem("Tools/Terrain Tools/Terrain Surface/Procedural Baker")]
        private static void OpenWindow()
        {
            GetWindow<TerrainSurfaceProceduralBakerWindow>("Terrain Surface Baker");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Procedural Terrain Texturing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Rules are evaluated in consistent world space. Higher priority rules claim weight first; " +
                "the fallback layer fills any unclaimed weight. Preview never changes TerrainData.",
                MessageType.Info);

            group = (TerrainSurfaceGroup)EditorGUILayout.ObjectField(
                "Terrain Group", group, typeof(TerrainSurfaceGroup), true);
            proceduralProfile = (TerrainSurfaceProceduralProfile)EditorGUILayout.ObjectField(
                "Procedural Profile", proceduralProfile, typeof(TerrainSurfaceProceduralProfile), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Procedural Profile"))
                {
                    CreateProceduralProfile();
                }
                using (new EditorGUI.DisabledScope(proceduralProfile == null))
                {
                    if (GUILayout.Button("Select Profile"))
                    {
                        Selection.activeObject = proceduralProfile;
                        EditorGUIUtility.PingObject(proceduralProfile);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Non-Destructive Preview", EditorStyles.boldLabel);
            previewTerrain = (Terrain)EditorGUILayout.ObjectField(
                "Preview Terrain", previewTerrain, typeof(Terrain), true);
            previewResolution = EditorGUILayout.IntSlider("Preview Resolution", previewResolution, 64, 512);
            using (new EditorGUI.DisabledScope(group == null || proceduralProfile == null))
            {
                if (GUILayout.Button("Generate Preview"))
                {
                    GeneratePreview();
                }
            }

            if (previewTexture != null)
            {
                float previewSize = Mathf.Min(position.width - 32f, 512f);
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake Alphamaps", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake computes every tile before writing. It then saves an automatic compressed alphamap backup " +
                "and applies all tiles. This operation intentionally does not use Unity's large TerrainData Undo payload.",
                MessageType.Warning);
            using (new EditorGUI.DisabledScope(group == null || proceduralProfile == null))
            {
                if (GUILayout.Button("Bake All Terrain Tiles"))
                {
                    BakeAllTerrains();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Restore Backup", EditorStyles.boldLabel);
            backupToRestore = (TerrainSurfaceAlphamapBackup)EditorGUILayout.ObjectField(
                "Backup", backupToRestore, typeof(TerrainSurfaceAlphamapBackup), false);
            using (new EditorGUI.DisabledScope(backupToRestore == null))
            {
                if (GUILayout.Button("Restore Backup Alphamaps"))
                {
                    RestoreBackupWithConfirmation(backupToRestore);
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                EditorGUILayout.HelpBox(status, statusType);
            }
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
        {
            DestroyPreview();
        }

        private void GeneratePreview()
        {
            if (!TerrainSurfaceProceduralBakeService.TryValidate(
                    group,
                    proceduralProfile,
                    out Terrain terrain,
                    out string error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            if (previewTerrain != null && ContainsTerrain(group, previewTerrain))
            {
                terrain = previewTerrain;
            }
            else
            {
                previewTerrain = terrain;
            }

            try
            {
                using TerrainSurfaceProceduralEvaluator evaluator =
                    new TerrainSurfaceProceduralEvaluator(group, proceduralProfile);
                float[,,] weights = evaluator.EvaluateTerrain(terrain, previewResolution, previewResolution);
                Color[] pixels = new Color[previewResolution * previewResolution];
                float[] pixelWeights = new float[evaluator.LayerCount];
                int cursor = 0;
                for (int y = 0; y < previewResolution; y++)
                {
                    for (int x = 0; x < previewResolution; x++)
                    {
                        for (int layer = 0; layer < pixelWeights.Length; layer++)
                        {
                            pixelWeights[layer] = weights[y, x, layer];
                        }
                        pixels[cursor++] = evaluator.EvaluatePreviewColor(pixelWeights);
                    }
                }

                DestroyPreview();
                previewTexture = new Texture2D(
                    previewResolution,
                    previewResolution,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true)
                {
                    name = "Terrain Surface Procedural Preview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                previewTexture.SetPixels(pixels);
                previewTexture.Apply(false, true);
                SetStatus($"Preview generated for '{terrain.name}' at {previewResolution}x{previewResolution}.", MessageType.Info);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
        }

        private void BakeAllTerrains()
        {
            if (!TerrainSurfaceProceduralBakeService.TryValidate(
                    group,
                    proceduralProfile,
                    out _,
                    out string error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Bake Terrain Surface Alphamaps",
                    $"This will replace the alphamaps on {group.Terrains.Count} terrain tile(s). " +
                    "An automatic compressed backup will be created first.",
                    "Bake All Tiles",
                    "Cancel"))
            {
                return;
            }

            try
            {
                backupToRestore = TerrainSurfaceProceduralBakeService.BakeAll(group, proceduralProfile);
                Selection.activeObject = backupToRestore;
                SetStatus(
                    $"Baked {group.Terrains.Count} tile(s). Backup: {AssetDatabase.GetAssetPath(backupToRestore)}",
                    MessageType.Info);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Bake cancelled before any alphamaps were changed.", MessageType.Info);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus(exception.Message, MessageType.Error);
            }
        }

        private void RestoreBackupWithConfirmation(TerrainSurfaceAlphamapBackup backup)
        {
            if (!EditorUtility.DisplayDialog(
                    "Restore Terrain Alphamaps",
                    $"Restore {backup.Entries.Count} TerrainData alphamap set(s) from backup created {backup.CreatedUtc}?",
                    "Restore",
                    "Cancel"))
            {
                return;
            }

            try
            {
                TerrainSurfaceProceduralBakeService.RestoreBackup(backup);
                AssetDatabase.SaveAssets();
                SetStatus($"Restored {backup.Entries.Count} terrain alphamap set(s).", MessageType.Info);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus($"Restore failed: {exception.Message}", MessageType.Error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void CreateProceduralProfile()
        {
            const string folder = TerrainToolsPaths.TerrainSurfaceGeneratedRoot;
            TerrainToolsPaths.EnsureAssetFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Procedural Terrain Profile",
                "TerrainProceduralProfile",
                "asset",
                "Choose where to save the procedural terrain profile.",
                folder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            TerrainSurfaceProceduralProfile created = CreateInstance<TerrainSurfaceProceduralProfile>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            proceduralProfile = created;
            Selection.activeObject = created;
            SetStatus("Created procedural profile. Add and configure rules in its Inspector.", MessageType.Info);
        }

        private static bool ContainsTerrain(TerrainSurfaceGroup terrainGroup, Terrain terrain)
        {
            for (int i = 0; i < terrainGroup.Terrains.Count; i++)
            {
                if (terrainGroup.Terrains[i] == terrain)
                {
                    return true;
                }
            }
            return false;
        }

        private void DestroyPreview()
        {
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
            Repaint();
        }
    }

    [CustomEditor(typeof(TerrainSurfaceAlphamapBackup))]
    internal sealed class TerrainSurfaceAlphamapBackupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            TerrainSurfaceAlphamapBackup backup = (TerrainSurfaceAlphamapBackup)target;
            if (GUILayout.Button("Restore Alphamaps From This Backup") &&
                EditorUtility.DisplayDialog(
                    "Restore Terrain Alphamaps",
                    $"Restore {backup.Entries.Count} TerrainData alphamap set(s)?",
                    "Restore",
                    "Cancel"))
            {
                try
                {
                    TerrainSurfaceProceduralBakeService.RestoreBackup(backup);
                    AssetDatabase.SaveAssets();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
    }
}
