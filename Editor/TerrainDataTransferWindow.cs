using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    public sealed class TerrainDataTransferWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Terrain Tools/Terrain Data Transfer";

        [SerializeField] private Terrain sourceTerrain;
        [SerializeField] private List<Terrain> targetTerrains = new();
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool copyTerrainLayers = true;
        [SerializeField] private bool copyDetailPrototypes = true;
        [SerializeField] private bool copyTreePrototypes = true;

        private Vector2 targetScrollPosition;

        [MenuItem(MenuPath, priority = 110)]
        private static void OpenWindow()
        {
            TerrainDataTransferWindow window = GetWindow<TerrainDataTransferWindow>();
            window.titleContent = new GUIContent("Terrain Data Transfer");
            window.minSize = new Vector2(500f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            targetTerrains ??= new List<Terrain>();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Data Transfer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Copies selected prototype lists from one Terrain to other loaded Terrains. " +
                "Existing target painting is preserved by matching Terrain Layer assets, " +
                "detail assets, and tree prefabs. Painting that uses definitions absent from " +
                "the source is removed. Source painting is not duplicated across tiles.",
                MessageType.Info);

            DrawSourceSection();
            EditorGUILayout.Space();
            DrawContentSection();
            EditorGUILayout.Space();
            DrawTargetsSection();
            EditorGUILayout.Space();
            DrawTransferButton();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Terrain nextSource = (Terrain)EditorGUILayout.ObjectField(
                "Source Terrain",
                sourceTerrain,
                typeof(Terrain),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                sourceTerrain = nextSource;
                RemoveSourceFromTargets();
            }

            if (sourceTerrain == null)
            {
                EditorGUILayout.HelpBox("Select the Terrain whose definitions are authoritative.", MessageType.Warning);
                return;
            }

            TerrainData sourceData = sourceTerrain.terrainData;
            if (sourceData == null)
            {
                EditorGUILayout.HelpBox("The source Terrain has no TerrainData.", MessageType.Error);
                return;
            }

            int layerCount = sourceData.terrainLayers?.Length ?? 0;
            int detailCount = sourceData.detailPrototypes?.Length ?? 0;
            int treeCount = sourceData.treePrototypes?.Length ?? 0;
            EditorGUILayout.LabelField(
                $"Source contains {layerCount} Terrain Layers, {detailCount} detail prototypes, " +
                $"and {treeCount} tree prototypes.",
                EditorStyles.miniLabel);
        }

        private void DrawContentSection()
        {
            EditorGUILayout.LabelField("Content To Copy", EditorStyles.boldLabel);
            copyTerrainLayers = EditorGUILayout.ToggleLeft(
                "Terrain Layers and their order",
                copyTerrainLayers);
            copyDetailPrototypes = EditorGUILayout.ToggleLeft(
                "Detail prototypes and settings",
                copyDetailPrototypes);
            copyTreePrototypes = EditorGUILayout.ToggleLeft(
                "Tree prototypes and settings",
                copyTreePrototypes);
        }

        private void DrawTargetsSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            includeInactive = EditorGUILayout.ToggleLeft(
                "Include inactive",
                includeInactive,
                GUILayout.Width(115f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find All In Loaded Scenes", GUILayout.Height(28f)))
            {
                FindTargetTerrains();
            }

            if (GUILayout.Button("Add Selected", GUILayout.Height(28f)))
            {
                AddSelectedTerrains();
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60f), GUILayout.Height(28f)))
            {
                targetTerrains.Clear();
            }

            EditorGUILayout.EndHorizontal();

            targetScrollPosition = EditorGUILayout.BeginScrollView(
                targetScrollPosition,
                EditorStyles.helpBox,
                GUILayout.MinHeight(145f),
                GUILayout.MaxHeight(240f));

            if (targetTerrains.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "Find terrains, add the current selection, or add a manual slot.",
                    EditorStyles.centeredGreyMiniLabel);
            }

            for (int i = 0; i < targetTerrains.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                targetTerrains[i] = (Terrain)EditorGUILayout.ObjectField(
                    targetTerrains[i],
                    typeof(Terrain),
                    true);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    targetTerrains.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Target Slot"))
            {
                targetTerrains.Add(null);
            }

            GetValidTargetCounts(out int terrainCount, out int terrainDataCount);
            if (terrainCount > 0)
            {
                string sharedDataNote = terrainCount == terrainDataCount
                    ? string.Empty
                    : $" ({terrainDataCount} unique TerrainData assets)";
                EditorGUILayout.LabelField(
                    $"{terrainCount} valid target Terrains{sharedDataNote}.",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawTransferButton()
        {
            TerrainDataTransferContent content = GetSelectedContent();
            GetValidTargetCounts(out _, out int terrainDataCount);

            string validationError = null;
            if (sourceTerrain == null || sourceTerrain.terrainData == null)
            {
                validationError = "Select a valid source Terrain.";
            }
            else
            {
                TerrainDataTransferService.TryValidateSource(
                    sourceTerrain.terrainData,
                    content,
                    out validationError);
            }

            if (validationError == null && terrainDataCount == 0)
            {
                validationError = "Add at least one target Terrain with different TerrainData.";
            }

            using (new EditorGUI.DisabledScope(validationError != null))
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 0.8f, 0.48f);
                if (GUILayout.Button(
                        $"Transfer Selected Data To {terrainDataCount} TerrainData Assets",
                        GUILayout.Height(40f)))
                {
                    ConfirmAndTransfer(content, terrainDataCount);
                }

                GUI.backgroundColor = previousColor;
            }

            if (validationError != null)
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }
        }

        private void ConfirmAndTransfer(
            TerrainDataTransferContent content,
            int terrainDataCount)
        {
            string categories = DescribeContent(content);
            bool confirmed = EditorUtility.DisplayDialog(
                "Transfer Terrain Data",
                $"This will replace {categories} on {terrainDataCount} target TerrainData assets.\n\n" +
                "Compatible target painting will be remapped by asset. Painting associated with " +
                "definitions that are not present on the source will be removed.\n\n" +
                "The operation supports Undo. Continue?",
                "Transfer",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            try
            {
                TerrainData sourceData = sourceTerrain.terrainData;
                TerrainDataTransferResult result =
                    TerrainDataTransferService.Transfer(sourceData, targetTerrains, content);

                int layerCount = (content & TerrainDataTransferContent.TerrainLayers) != 0
                    ? sourceData.terrainLayers.Length
                    : 0;
                int detailCount = (content & TerrainDataTransferContent.DetailPrototypes) != 0
                    ? sourceData.detailPrototypes.Length
                    : 0;
                int treeCount = (content & TerrainDataTransferContent.TreePrototypes) != 0
                    ? sourceData.treePrototypes.Length
                    : 0;

                string message =
                    $"Updated {result.TerrainDataCount} TerrainData assets used by " +
                    $"{result.TerrainCount} Terrains.\n\n" +
                    $"Copied: {layerCount} layers, {detailCount} detail prototypes, " +
                    $"{treeCount} tree prototypes.\n" +
                    $"Removed as incompatible: {result.RemovedTerrainLayers} layer definitions, " +
                    $"{result.RemovedDetailPrototypes} detail definitions, " +
                    $"{result.RemovedTreeInstances} tree instances.";

                Debug.Log($"[Terrain Data Transfer] {message.Replace(Environment.NewLine, " ")}");
                EditorUtility.DisplayDialog("Terrain Data Transfer Complete", message, "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Terrain Data Transfer Failed",
                    $"{exception.Message}\n\nNo partial changes were kept.",
                    "OK");
            }
        }

        private void FindTargetTerrains()
        {
            targetTerrains.Clear();
            FindObjectsInactive inactiveMode = includeInactive
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;
            Terrain[] terrains =
                UnityEngine.Object.FindObjectsByType<Terrain>(inactiveMode);

            for (int i = 0; i < terrains.Length; i++)
            {
                AddTargetIfValid(terrains[i]);
            }

            SortTargets();
            Repaint();
        }

        private void AddSelectedTerrains()
        {
            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
            {
                Terrain[] terrains = selection[i].GetComponentsInChildren<Terrain>(true);
                for (int terrainIndex = 0; terrainIndex < terrains.Length; terrainIndex++)
                {
                    AddTargetIfValid(terrains[terrainIndex]);
                }
            }

            SortTargets();
            Repaint();
        }

        private void AddTargetIfValid(Terrain terrain)
        {
            if (!IsValidSceneTerrain(terrain)
                || HasSourceTerrainData(terrain)
                || targetTerrains.Contains(terrain))
            {
                return;
            }

            targetTerrains.Add(terrain);
        }

        private bool IsValidSceneTerrain(Terrain terrain)
        {
            return terrain != null
                && terrain.terrainData != null
                && terrain.gameObject.scene.IsValid()
                && (includeInactive || terrain.gameObject.activeInHierarchy);
        }

        private bool HasSourceTerrainData(Terrain terrain)
        {
            return sourceTerrain != null
                && sourceTerrain.terrainData != null
                && terrain != null
                && terrain.terrainData == sourceTerrain.terrainData;
        }

        private void RemoveSourceFromTargets()
        {
            for (int i = targetTerrains.Count - 1; i >= 0; i--)
            {
                if (HasSourceTerrainData(targetTerrains[i]))
                {
                    targetTerrains.RemoveAt(i);
                }
            }
        }

        private void GetValidTargetCounts(
            out int terrainCount,
            out int terrainDataCount)
        {
            HashSet<Terrain> terrains = new();
            HashSet<TerrainData> data = new();

            for (int i = 0; i < targetTerrains.Count; i++)
            {
                Terrain terrain = targetTerrains[i];
                if (terrain == null
                    || terrain.terrainData == null
                    || HasSourceTerrainData(terrain)
                    || !terrain.gameObject.scene.IsValid()
                    || !terrains.Add(terrain))
                {
                    continue;
                }

                data.Add(terrain.terrainData);
            }

            terrainCount = terrains.Count;
            terrainDataCount = data.Count;
        }

        private void SortTargets()
        {
            targetTerrains.Sort((left, right) =>
            {
                if (left == null)
                {
                    return right == null ? 0 : 1;
                }

                if (right == null)
                {
                    return -1;
                }

                int sceneComparison = string.Compare(
                    left.gameObject.scene.path,
                    right.gameObject.scene.path,
                    StringComparison.OrdinalIgnoreCase);
                return sceneComparison != 0
                    ? sceneComparison
                    : string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private TerrainDataTransferContent GetSelectedContent()
        {
            TerrainDataTransferContent content = TerrainDataTransferContent.None;
            if (copyTerrainLayers)
            {
                content |= TerrainDataTransferContent.TerrainLayers;
            }

            if (copyDetailPrototypes)
            {
                content |= TerrainDataTransferContent.DetailPrototypes;
            }

            if (copyTreePrototypes)
            {
                content |= TerrainDataTransferContent.TreePrototypes;
            }

            return content;
        }

        private static string DescribeContent(TerrainDataTransferContent content)
        {
            List<string> categories = new();
            if ((content & TerrainDataTransferContent.TerrainLayers) != 0)
            {
                categories.Add("Terrain Layers");
            }

            if ((content & TerrainDataTransferContent.DetailPrototypes) != 0)
            {
                categories.Add("detail prototypes");
            }

            if ((content & TerrainDataTransferContent.TreePrototypes) != 0)
            {
                categories.Add("tree prototypes");
            }

            return string.Join(", ", categories);
        }
    }
}
