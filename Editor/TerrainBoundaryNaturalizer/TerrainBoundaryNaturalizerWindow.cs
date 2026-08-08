using System;
using System.Collections.Generic;
using Dominikozi.TerrainTools;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal sealed class TerrainBoundaryNaturalizerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Terrain Tools/Terrain Boundary Naturalizer";
        private const float BrushPreviewOffset = 0.04f;

        [SerializeField] private TerrainSurfaceGroup terrainGroup;
        [SerializeField] private bool noiseFoldout;

        private TerrainBoundaryNaturalizerSettings settings;
        private TerrainBoundaryStroke activeStroke;
        private Vector2 scrollPosition;
        private string statusMessage;
        private MessageType statusType;

        [MenuItem(MenuPath, priority = 2413)]
        private static void Open()
        {
            TerrainBoundaryNaturalizerWindow window = GetWindow<TerrainBoundaryNaturalizerWindow>(
                "Boundary Naturalizer");
            window.minSize = new Vector2(430f, 590f);
            window.Show();
        }

        private void OnEnable()
        {
            settings = TerrainBoundaryNaturalizerState.instance.Settings;
            SceneView.duringSceneGui += DuringSceneGui;
            Selection.selectionChanged += HandleSelectionChanged;
            Undo.undoRedoPerformed += HandleUndoRedo;
            TryAssignGroupFromSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            Selection.selectionChanged -= HandleSelectionChanged;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            activeStroke = null;
            TerrainBoundaryNaturalizerState.instance.SaveSettings();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            settings ??= TerrainBoundaryNaturalizerState.instance.Settings;
            settings.Sanitize();
            EditorGUI.BeginChangeCheck();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.HelpBox(
                "Drag the left mouse button over an existing Terrain Layer boundary. The alphamap is "
                + "recalculated once when the mouse button is released. Height blending, terrain height, "
                + "and details remain unchanged.",
                MessageType.Info);

            DrawTargetSection();
            DrawLayerSection();
            DrawBoundarySection();
            DrawBrushSection();
            DrawValidation();
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck())
            {
                settings.Sanitize();
                TerrainBoundaryNaturalizerState.instance.SaveSettings();
                SceneView.RepaintAll();
            }
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Terrain group", EditorStyles.boldLabel);
            TerrainSurfaceGroup newGroup = (TerrainSurfaceGroup)EditorGUILayout.ObjectField(
                "Terrain Surface Group",
                terrainGroup,
                typeof(TerrainSurfaceGroup),
                true);
            if (newGroup != terrainGroup)
            {
                AssignGroup(newGroup);
            }

            if (GUILayout.Button("Use Selected Terrain or Group"))
            {
                TerrainSurfaceGroup selected = TerrainBoundaryNaturalizerService.FindGroupForSelection();
                if (selected != null)
                {
                    AssignGroup(selected);
                }
                else
                {
                    SetStatus("The selection does not belong to a Terrain Surface Group.", MessageType.Warning);
                }
            }
        }

        private void DrawLayerSection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Boundaries", EditorStyles.boldLabel);
            settings.LayerScope = (TerrainBoundaryLayerScope)EditorGUILayout.EnumPopup(
                "Layer Scope",
                settings.LayerScope);
            if (settings.LayerScope == TerrainBoundaryLayerScope.SelectedPair)
            {
                settings.LayerA = (TerrainLayer)EditorGUILayout.ObjectField(
                    "Layer A",
                    settings.LayerA,
                    typeof(TerrainLayer),
                    false);
                settings.LayerB = (TerrainLayer)EditorGUILayout.ObjectField(
                    "Layer B",
                    settings.LayerB,
                    typeof(TerrainLayer),
                    false);
            }

            settings.Character = (TerrainBoundaryCharacter)EditorGUILayout.EnumPopup(
                "Character",
                settings.Character);
            settings.EdgeContrast = EditorGUILayout.Slider(
                new GUIContent(
                    "Edge Contrast",
                    "0 preserves the weight profile. Higher values narrow the dominant pair transition."),
                settings.EdgeContrast,
                0f,
                1f);

            if (settings.Character == TerrainBoundaryCharacter.Islands)
            {
                if (settings.LayerScope == TerrainBoundaryLayerScope.SelectedPair)
                {
                    settings.IslandSource = (TerrainBoundaryIslandSource)EditorGUILayout.EnumPopup(
                        "Island Source",
                        settings.IslandSource);
                }

                settings.IslandSize = EditorGUILayout.Slider("Island Size (m)", settings.IslandSize, 0.1f, 4f);
                settings.IslandReach = EditorGUILayout.Slider("Island Reach (m)", settings.IslandReach, 0.1f, 5f);
                settings.IslandAmount = EditorGUILayout.Slider("Island Amount", settings.IslandAmount, 0f, 1f);
            }
        }

        private void DrawBoundarySection()
        {
            EditorGUILayout.Space(10f);
            noiseFoldout = EditorGUILayout.Foldout(noiseFoldout, "Domain-warped noise", true);
            if (!noiseFoldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawNoiseBand(
                    "Large",
                    refValue => settings.LargeFeatureSize = refValue,
                    settings.LargeFeatureSize,
                    2f,
                    64f,
                    refValue => settings.LargeDisplacement = refValue,
                    settings.LargeDisplacement,
                    0f,
                    5f);
                DrawNoiseBand(
                    "Medium",
                    refValue => settings.MediumFeatureSize = refValue,
                    settings.MediumFeatureSize,
                    0.5f,
                    16f,
                    refValue => settings.MediumDisplacement = refValue,
                    settings.MediumDisplacement,
                    0f,
                    2f);
                DrawNoiseBand(
                    "Small",
                    refValue => settings.SmallFeatureSize = refValue,
                    settings.SmallFeatureSize,
                    0.1f,
                    4f,
                    refValue => settings.SmallDisplacement = refValue,
                    settings.SmallDisplacement,
                    0f,
                    0.5f);
            }
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Scene View Brush", EditorStyles.boldLabel);
            bool paintingEnabled = EditorGUILayout.ToggleLeft(
                "Enable Naturalization in Scene View",
                settings.PaintingEnabled,
                EditorStyles.toolbarButton);
            if (paintingEnabled != settings.PaintingEnabled)
            {
                settings.PaintingEnabled = paintingEnabled;
                if (!paintingEnabled)
                {
                    activeStroke = null;
                }
            }

            settings.BrushDiameter = EditorGUILayout.Slider(
                "Size (Diameter)",
                settings.BrushDiameter,
                1f,
                256f);
            settings.BrushFalloff = EditorGUILayout.Slider(
                "Soft Edge",
                settings.BrushFalloff,
                0f,
                1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                settings.Seed = EditorGUILayout.IntField("Seed", settings.Seed);
                if (GUILayout.Button("Randomize", GUILayout.Width(80f)))
                {
                    settings.Seed = Guid.NewGuid().GetHashCode();
                }
            }

            if (settings.PaintingEnabled)
            {
                EditorGUILayout.HelpBox(
                    "The left mouse button starts a stroke. The result appears when the button is released. "
                    + "Alt + left mouse button controls the camera. The complete drag is one Undo operation.",
                    MessageType.None);
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.Space(8f);
            if (!TerrainBoundaryNaturalizerService.TryValidate(
                    terrainGroup,
                    settings,
                    out List<Terrain> terrains,
                    out _,
                    out _,
                    out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            int colliderCount = 0;
            for (int i = 0; i < terrains.Count; i++)
            {
                if (terrains[i].GetComponent<TerrainCollider>() != null)
                {
                    colliderCount++;
                }
            }

            if (colliderCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Terrain in the group has a TerrainCollider, so the brush cannot read the cursor.",
                    MessageType.Error);
                return;
            }

            int layerCount = terrains[0].terrainData.terrainLayers.Length;
            EditorGUILayout.HelpBox(
                $"Ready: {terrains.Count} tile(s), {layerCount} layer(s). Noise is evaluated in world space.",
                MessageType.Info);
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (activeStroke != null && current.rawType == EventType.MouseUp && current.button == 0)
            {
                FinishStroke();
                if (current.type == EventType.MouseUp)
                {
                    current.Use();
                }
                return;
            }

            if (!settings.PaintingEnabled || current.alt || terrainGroup == null)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!TerrainBoundaryNaturalizerService.TryRaycast(
                    terrainGroup,
                    ray,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal))
            {
                DrawActiveStroke();
                return;
            }

            DrawBrushPreview(hitPoint, hitNormal);
            DrawActiveStroke();
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                sceneView.Repaint();
            }

            if (current.button != 0)
            {
                return;
            }

            if (current.type == EventType.MouseDown)
            {
                if (!TerrainBoundaryNaturalizerService.TryValidate(
                        terrainGroup,
                        settings,
                        out _,
                        out _,
                        out _,
                        out string error))
                {
                    SetStatus(error, MessageType.Error);
                    Repaint();
                    return;
                }

                activeStroke = new TerrainBoundaryStroke();
                activeStroke.AddPoint(hitPoint);
                statusMessage = null;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && activeStroke != null)
            {
                activeStroke.AddPoint(hitPoint);
                current.Use();
            }
        }

        private void FinishStroke()
        {
            TerrainBoundaryStroke stroke = activeStroke;
            activeStroke = null;
            if (stroke == null || stroke.PointCount == 0)
            {
                return;
            }

            try
            {
                TerrainBoundaryNaturalizationSummary summary = TerrainBoundaryNaturalizerService.Naturalize(
                    terrainGroup,
                    stroke,
                    settings);
                SetStatus(
                    summary.TilesChanged > 0
                        ? $"Done: changed {summary.TilesChanged} tile(s) and {summary.TexelsChanged} texel(s). "
                            + "Undo reverts the complete stroke."
                        : "The stroke did not find a boundary that matches the current settings.",
                    summary.TilesChanged > 0 ? MessageType.Info : MessageType.Warning);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Operation cancelled. TerrainData was not changed.", MessageType.Info);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("Naturalization failed. See the Console for details.", MessageType.Error);
            }
            finally
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void DrawBrushPreview(Vector3 hitPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up;
            Vector3 center = hitPoint + normal * BrushPreviewOffset;
            Color previous = Handles.color;
            Handles.color = new Color(0.1f, 0.8f, 1f, 0.12f);
            Handles.DrawSolidDisc(center, normal, settings.BrushRadius);
            Handles.color = new Color(0.1f, 0.85f, 1f, 0.95f);
            Handles.DrawWireDisc(center, normal, settings.BrushRadius);
            Handles.color = previous;
        }

        private void DrawActiveStroke()
        {
            if (activeStroke == null || activeStroke.PointCount < 2 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3[] points = new Vector3[activeStroke.PointCount];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = activeStroke.Points[i] + Vector3.up * BrushPreviewOffset;
            }

            Color previous = Handles.color;
            Handles.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            Handles.DrawAAPolyLine(3f, points);
            Handles.color = previous;
        }

        private void HandleSelectionChanged()
        {
            TryAssignGroupFromSelection();
            Repaint();
        }

        private void TryAssignGroupFromSelection()
        {
            TerrainSurfaceGroup selected = TerrainBoundaryNaturalizerService.FindGroupForSelection();
            if (selected != null && selected != terrainGroup)
            {
                AssignGroup(selected);
            }
        }

        private void AssignGroup(TerrainSurfaceGroup group)
        {
            activeStroke = null;
            terrainGroup = group;
            EnsureDefaultPair();
            statusMessage = null;
            SceneView.RepaintAll();
            Repaint();
        }

        private void EnsureDefaultPair()
        {
            if (terrainGroup == null || terrainGroup.Terrains.Count == 0)
            {
                return;
            }

            Terrain firstTerrain = null;
            for (int i = 0; i < terrainGroup.Terrains.Count; i++)
            {
                if (terrainGroup.Terrains[i] != null && terrainGroup.Terrains[i].terrainData != null)
                {
                    firstTerrain = terrainGroup.Terrains[i];
                    break;
                }
            }

            if (firstTerrain == null)
            {
                return;
            }

            TerrainLayer[] layers = firstTerrain.terrainData.terrainLayers;
            if (layers.Length < 2)
            {
                return;
            }

            if (Array.IndexOf(layers, settings.LayerA) < 0)
            {
                settings.LayerA = layers[0];
            }
            if (Array.IndexOf(layers, settings.LayerB) < 0 || settings.LayerB == settings.LayerA)
            {
                settings.LayerB = layers[1];
            }
        }

        private void HandleUndoRedo()
        {
            if (terrainGroup != null)
            {
                for (int i = 0; i < terrainGroup.Terrains.Count; i++)
                {
                    terrainGroup.Terrains[i]?.Flush();
                }
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }

        private static void DrawNoiseBand(
            string label,
            Action<float> setFeatureSize,
            float featureSize,
            float minimumFeature,
            float maximumFeature,
            Action<float> setDisplacement,
            float displacement,
            float minimumDisplacement,
            float maximumDisplacement)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            setFeatureSize(EditorGUILayout.Slider(
                "Feature size (m)",
                featureSize,
                minimumFeature,
                maximumFeature));
            setDisplacement(EditorGUILayout.Slider(
                "Displacement (m)",
                displacement,
                minimumDisplacement,
                maximumDisplacement));
        }
    }
}
