#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

public sealed class CompositeTerrainPainterWindow : EditorWindow
{
    private const string MenuPath = "Tools/Terrain Tools/Painters/Composite Layer Painter";
    private const string UndoName = "Composite Terrain Paint";
    private const float MinBrushSize = 0.25f;
    private const float MaxBrushSize = 256f;
    private const float BrushPreviewOffset = 0.04f;
    private const float CoverageThresholdWidth = 0.15f;

    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private CompositeTerrainPaintPreset preset;
    [SerializeField] private bool paintingEnabled = true;
    [SerializeField] private float brushSize = 8f;
    [SerializeField] private float brushStrength = 0.35f;
    [SerializeField] private float brushFalloff = 0.75f;

    private SerializedObject presetSerializedObject;
    private SerializedProperty entriesProperty;
    private ReorderableList entriesList;
    private bool isPainting;
    private readonly TerrainPaintUndoTransaction undoTransaction = new();

    private float BrushRadius => Mathf.Max(MinBrushSize, brushSize) * 0.5f;

    [MenuItem(MenuPath, priority = 2410)]
    private static void Open()
    {
        CompositeTerrainPainterWindow window = GetWindow<CompositeTerrainPainterWindow>("Composite Layer Painter");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
        Undo.undoRedoPerformed += HandleUndoRedoPerformed;
        TryUseSelectedTerrain();
        RebuildPresetSerializedObject();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
        EndPaintStroke();
    }

    private void OnSelectionChange()
    {
        if (targetTerrain == null)
        {
            TryUseSelectedTerrain();
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        Terrain newTarget = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck())
        {
            targetTerrain = newTarget;
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();
        CompositeTerrainPaintPreset newPreset = (CompositeTerrainPaintPreset)EditorGUILayout.ObjectField(
            "Preset",
            preset,
            typeof(CompositeTerrainPaintPreset),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            preset = newPreset;
            RebuildPresetSerializedObject();
            SceneView.RepaintAll();
        }

        DrawPresetButtons();

        EditorGUILayout.Space(8f);
        EditorGUI.BeginChangeCheck();
        paintingEnabled = EditorGUILayout.ToggleLeft("Enable Scene Painting", paintingEnabled, EditorStyles.toolbarButton);
        if (EditorGUI.EndChangeCheck())
        {
            if (!paintingEnabled)
            {
                EndPaintStroke();
            }

            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8f);
        brushSize = EditorGUILayout.Slider("Brush Size", brushSize, MinBrushSize, MaxBrushSize);
        brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);
        brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0f, 1f);

        EditorGUILayout.Space(8f);
        DrawValidationMessages();

        EditorGUILayout.Space(8f);
        DrawPresetEntries();
    }

    private void DrawPresetButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Preset"))
            {
                CreateNewPreset();
            }

            using (new EditorGUI.DisabledScope(preset == null))
            {
                if (GUILayout.Button("Save Preset"))
                {
                    SavePreset();
                }
            }

            if (GUILayout.Button("Load/Use Preset"))
            {
                LoadSelectedPreset();
            }
        }
    }

    private void DrawValidationMessages()
    {
        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Assign a Terrain before painting.", MessageType.Info);
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData == null)
        {
            EditorGUILayout.HelpBox("The selected Terrain has no TerrainData.", MessageType.Warning);
            return;
        }

        if (terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
        {
            EditorGUILayout.HelpBox("The selected TerrainData has no TerrainLayers assigned.", MessageType.Warning);
        }

        if (preset == null)
        {
            EditorGUILayout.HelpBox("Create or assign a CompositeTerrainPaintPreset.", MessageType.Info);
            return;
        }

        if (preset.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox("The preset has no blend entries.", MessageType.Info);
            return;
        }

        int missingReferenceCount = preset.Entries.Count(entry => entry.enabled && entry.layer == null);
        if (missingReferenceCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{missingReferenceCount} blend entry references a missing TerrainLayer.",
                MessageType.Warning);
        }

        List<TerrainLayer> missingLayers = GetMissingLayers(targetTerrain, preset);
        if (missingLayers.Count <= 0)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Some preset TerrainLayers are not assigned to the selected TerrainData:\n" +
            string.Join("\n", missingLayers.Select(layer => $"- {layer.name}")) +
            "\n\nPainting is disabled until missing layers are added, so the preset cannot be applied partially.",
            MessageType.Warning);

        if (GUILayout.Button("Add Missing Layers To Terrain"))
        {
            EnsureLayersExist(targetTerrain, preset);
        }
    }

    private void DrawPresetEntries()
    {
        EditorGUILayout.LabelField("Blend Entries", EditorStyles.boldLabel);

        if (preset == null || presetSerializedObject == null || entriesProperty == null || entriesList == null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button("Add Blend Entry");
            }

            return;
        }

        presetSerializedObject.Update();
        entriesList.DoLayoutList();

        if (presetSerializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(preset);
            SceneView.RepaintAll();
        }
    }

    private void DrawBlendEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        SerializedProperty enabled = entry.FindPropertyRelative("enabled");
        SerializedProperty layer = entry.FindPropertyRelative("layer");
        SerializedProperty weight = entry.FindPropertyRelative("weight");
        SerializedProperty coverage = entry.FindPropertyRelative("coverage");
        SerializedProperty noiseScale = entry.FindPropertyRelative("noiseScale");
        SerializedProperty noiseInfluence = entry.FindPropertyRelative("noiseInfluence");
        SerializedProperty seed = entry.FindPropertyRelative("seed");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect line = new(rect.x, rect.y + spacing, rect.width, lineHeight);

        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            EditorGUI.LabelField(line, $"Entry {index + 1}", EditorStyles.boldLabel);
        }

        Rect enabledRect = new(rect.x + rect.width - 90f, line.y, 90f, lineHeight);
        EditorGUI.PropertyField(enabledRect, enabled, GUIContent.none);

        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            line.y += lineHeight + spacing;
            Rect selectorRect = new(line.x, line.y, line.width, TerrainPrototypePicker.SelectorHeight);
            DrawTerrainLayerSelector(selectorRect, index, layer);

            line.y += TerrainPrototypePicker.SelectorHeight + spacing;
            weight.floatValue = Mathf.Max(0f, EditorGUI.FloatField(line, "Weight", weight.floatValue));

            line.y += lineHeight + spacing;
            coverage.floatValue = EditorGUI.Slider(line, "Coverage", coverage.floatValue, 0f, 1f);

            line.y += lineHeight + spacing;
            noiseScale.floatValue = Mathf.Max(0.001f, EditorGUI.FloatField(line, "Noise Scale", noiseScale.floatValue));

            line.y += lineHeight + spacing;
            noiseInfluence.floatValue = EditorGUI.Slider(line, "Noise Influence", noiseInfluence.floatValue, 0f, 1f);

            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, seed);
        }
    }

    private void DuringSceneGui(SceneView sceneView)
    {
        Event current = Event.current;
        if (!paintingEnabled)
        {
            if (current != null && current.rawType == EventType.MouseUp)
            {
                EndPaintStroke();
            }

            return;
        }

        if (current == null || current.alt || targetTerrain == null)
        {
            if (current != null && current.rawType == EventType.MouseUp)
            {
                EndPaintStroke();
            }

            return;
        }

        if (!TryGetTerrainHit(current.mousePosition, targetTerrain, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            if (current.rawType == EventType.MouseUp)
            {
                EndPaintStroke();
            }

            return;
        }

        DrawBrushPreview(hitPoint, hitNormal);
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

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
            BeginPaintStroke();
            PaintDab(hitPoint);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && isPainting)
        {
            PaintDab(hitPoint);
            current.Use();
        }
        else if (current.rawType == EventType.MouseUp)
        {
            EndPaintStroke();
        }
    }

    private void PaintDab(Vector3 hitPoint)
    {
        try
        {
            PaintAt(targetTerrain, preset, hitPoint, brushSize, brushStrength, brushFalloff);
        }
        catch (Exception exception)
        {
            AbortPaintStroke(exception);
        }
    }

    private void BeginPaintStroke()
    {
        if (isPainting || targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        undoTransaction.Begin(UndoName);
        isPainting = true;
    }

    private void EndPaintStroke()
    {
        if (!isPainting)
        {
            return;
        }

        undoTransaction.Complete();
        isPainting = false;
    }

    private void AbortPaintStroke(Exception exception)
    {
        undoTransaction.Revert();
        isPainting = false;
        Debug.LogException(exception);
    }

    private void RegisterTerrainUndo(Terrain terrain)
    {
        TerrainData terrainData = terrain != null ? terrain.terrainData : null;
        undoTransaction.Register(terrainData);
    }

    private void HandleUndoRedoPerformed()
    {
        Terrain[] activeTerrains = Terrain.activeTerrains;
        if (activeTerrains != null)
        {
            foreach (Terrain terrain in activeTerrains)
            {
                terrain?.Flush();
            }
        }

        targetTerrain?.Flush();
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawBrushPreview(Vector3 hitPoint, Vector3 hitNormal)
    {
        float radius = BrushRadius;
        Vector3 normal = hitNormal.sqrMagnitude > 0f ? hitNormal.normalized : Vector3.up;
        Vector3 center = hitPoint + normal * BrushPreviewOffset;

        Color previous = Handles.color;
        Handles.color = new Color(0.1f, 0.65f, 1f, 0.12f);
        Handles.DrawSolidDisc(center, normal, radius);
        Handles.color = new Color(0.1f, 0.75f, 1f, 0.9f);
        Handles.DrawWireDisc(center, normal, radius);
        Handles.color = previous;
    }

    private void TryUseSelectedTerrain()
    {
        if (Selection.activeGameObject == null)
        {
            return;
        }

        Terrain selectedTerrain = Selection.activeGameObject.GetComponent<Terrain>();
        if (selectedTerrain != null)
        {
            targetTerrain = selectedTerrain;
        }
    }

    private void RebuildPresetSerializedObject()
    {
        presetSerializedObject = preset != null ? new SerializedObject(preset) : null;
        entriesProperty = presetSerializedObject?.FindProperty("entries");
        entriesList = entriesProperty != null ? CreateEntriesList() : null;
    }

    private ReorderableList CreateEntriesList()
    {
        ReorderableList list = new(presetSerializedObject, entriesProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Blend Entries"),
            drawElementCallback = DrawBlendEntry,
            elementHeightCallback = _ =>
            {
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                return (lineHeight * 6f) + TerrainPrototypePicker.SelectorHeight + (spacing * 8f);
            },
            onAddCallback = _ => ShowLayerPicker(TerrainPrototypePicker.GetCurrentEventAnchor(), -1),
            onRemoveCallback = reorderableList =>
            {
                if (reorderableList.index < 0 || reorderableList.index >= entriesProperty.arraySize)
                {
                    return;
                }

                entriesProperty.DeleteArrayElementAtIndex(reorderableList.index);
            }
        };

        return list;
    }

    private void CreateNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Composite Terrain Paint Preset",
            "CompositeTerrainPaintPreset",
            "asset",
            "Choose where to save the new terrain paint preset.");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        CompositeTerrainPaintPreset newPreset = CreateInstance<CompositeTerrainPaintPreset>();
        AssetDatabase.CreateAsset(newPreset, path);
        AssetDatabase.SaveAssets();
        preset = newPreset;
        RebuildPresetSerializedObject();
        EditorGUIUtility.PingObject(preset);
    }

    private void SavePreset()
    {
        if (preset == null)
        {
            return;
        }

        if (presetSerializedObject != null)
        {
            presetSerializedObject.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
    }

    private void LoadSelectedPreset()
    {
        CompositeTerrainPaintPreset selectedPreset = Selection.activeObject as CompositeTerrainPaintPreset;
        if (selectedPreset != null)
        {
            preset = selectedPreset;
            RebuildPresetSerializedObject();
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        EditorUtility.DisplayDialog(
            "Load Preset",
            "Select a CompositeTerrainPaintPreset asset in the Project window, then click Load/Use Preset.",
            "OK");
    }

    private void DrawTerrainLayerSelector(Rect rect, int entryIndex, SerializedProperty layerProperty)
    {
        TerrainLayer layer = layerProperty.objectReferenceValue as TerrainLayer;
        int selectedIndex = GetLayerIndex(targetTerrain != null ? targetTerrain.terrainData : null, layer);
        string displayName = layer != null ? layer.name : string.Empty;
        if (layer != null && selectedIndex < 0)
        {
            displayName += " (not on Terrain)";
        }

        if (TerrainPrototypePicker.DrawSelector(
                rect,
                new GUIContent("Terrain Layer"),
                displayName,
                layer,
                layer != null ? layer.diffuseTexture : null,
                layer != null,
                "Choose one of the TerrainLayers assigned to the selected Terrain."))
        {
            ShowLayerPicker(rect, entryIndex);
        }
    }

    private void ShowLayerPicker(Rect anchorRect, int entryIndex)
    {
        TerrainData terrainData = targetTerrain != null ? targetTerrain.terrainData : null;
        TerrainLayer[] terrainLayers = terrainData != null
            ? terrainData.terrainLayers ?? Array.Empty<TerrainLayer>()
            : Array.Empty<TerrainLayer>();
        HashSet<TerrainLayer> usedLayers = new();
        TerrainLayer selectedLayer = null;

        if (entriesProperty != null)
        {
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                TerrainLayer layer = entriesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("layer")
                    .objectReferenceValue as TerrainLayer;
                if (i == entryIndex)
                {
                    selectedLayer = layer;
                }
                else if (layer != null)
                {
                    usedLayers.Add(layer);
                }
            }
        }

        List<TerrainPrototypePickerOption> options = new(terrainLayers.Length);
        int selectedIndex = -1;
        for (int i = 0; i < terrainLayers.Length; i++)
        {
            TerrainLayer layer = terrainLayers[i];
            bool alreadyUsed = layer != null && usedLayers.Contains(layer);
            bool enabled = layer != null && !alreadyUsed;
            if (layer == selectedLayer)
            {
                selectedIndex = i;
            }

            options.Add(new TerrainPrototypePickerOption(
                i,
                layer != null ? layer.name : $"Missing Layer {i + 1}",
                $"Layer {i + 1}",
                layer,
                layer != null ? layer.diffuseTexture : null,
                enabled,
                layer == null ? "This TerrainLayer reference is missing." : alreadyUsed ? "Already used by this preset." : null));
        }

        TerrainPrototypePicker.Show(
            anchorRect,
            "Select Terrain Layer",
            targetTerrain != null ? targetTerrain.name : null,
            options,
            selectedIndex,
            prototypeIndex =>
            {
                if (entryIndex < 0)
                {
                    AddBlendEntry(prototypeIndex);
                }
                else
                {
                    AssignLayerToEntry(entryIndex, prototypeIndex);
                }
            });
    }

    private void AddBlendEntry(int terrainLayerIndex)
    {
        TerrainLayer[] terrainLayers = targetTerrain != null && targetTerrain.terrainData != null
            ? targetTerrain.terrainData.terrainLayers
            : null;
        if (entriesProperty == null
            || terrainLayers == null
            || terrainLayerIndex < 0
            || terrainLayerIndex >= terrainLayers.Length
            || terrainLayers[terrainLayerIndex] == null)
        {
            return;
        }

        presetSerializedObject.Update();
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);

        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("enabled").boolValue = true;
        entry.FindPropertyRelative("layer").objectReferenceValue = terrainLayers[terrainLayerIndex];
        entry.FindPropertyRelative("weight").floatValue = 1f;
        entry.FindPropertyRelative("coverage").floatValue = 1f;
        entry.FindPropertyRelative("noiseScale").floatValue = 16f;
        entry.FindPropertyRelative("noiseInfluence").floatValue = 1f;
        entry.FindPropertyRelative("seed").intValue = 0;
        ApplyPresetPickerChange();
    }

    private void AssignLayerToEntry(int entryIndex, int terrainLayerIndex)
    {
        TerrainLayer[] terrainLayers = targetTerrain != null && targetTerrain.terrainData != null
            ? targetTerrain.terrainData.terrainLayers
            : null;
        if (entriesProperty == null
            || terrainLayers == null
            || entryIndex < 0
            || entryIndex >= entriesProperty.arraySize
            || terrainLayerIndex < 0
            || terrainLayerIndex >= terrainLayers.Length
            || terrainLayers[terrainLayerIndex] == null)
        {
            return;
        }

        presetSerializedObject.Update();
        entriesProperty
            .GetArrayElementAtIndex(entryIndex)
            .FindPropertyRelative("layer")
            .objectReferenceValue = terrainLayers[terrainLayerIndex];
        ApplyPresetPickerChange();
    }

    private void ApplyPresetPickerChange()
    {
        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        RebuildPresetSerializedObject();
        Repaint();
        SceneView.RepaintAll();
    }

    private static bool TryGetTerrainHit(
        Vector2 guiMousePosition,
        Terrain terrain,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        hitPoint = default;
        hitNormal = Vector3.up;

        TerrainData terrainData = terrain.terrainData;
        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainData == null || terrainCollider == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(guiMousePosition);
        if (!terrainCollider.Raycast(ray, out RaycastHit hit, float.MaxValue))
        {
            return false;
        }

        hitPoint = hit.point;
        if (WorldToTerrainNormalized(terrain, hitPoint, out Vector2 normalized))
        {
            hitNormal = terrain.transform.TransformDirection(
                terrainData.GetInterpolatedNormal(normalized.x, normalized.y)).normalized;
        }
        else
        {
            hitNormal = hit.normal;
        }

        return true;
    }

    private static List<TerrainLayer> GetMissingLayers(Terrain terrain, CompositeTerrainPaintPreset paintPreset)
    {
        List<TerrainLayer> missingLayers = new();
        if (terrain == null || terrain.terrainData == null || paintPreset == null)
        {
            return missingLayers;
        }

        TerrainLayer[] terrainLayers = terrain.terrainData.terrainLayers ?? Array.Empty<TerrainLayer>();
        foreach (CompositeTerrainPaintPreset.Entry entry in paintPreset.Entries)
        {
            if (!entry.enabled)
            {
                continue;
            }

            if (entry.layer == null || terrainLayers.Contains(entry.layer) || missingLayers.Contains(entry.layer))
            {
                continue;
            }

            missingLayers.Add(entry.layer);
        }

        return missingLayers;
    }

    private static int GetLayerIndex(TerrainData terrainData, TerrainLayer layer)
    {
        if (terrainData == null || layer == null)
        {
            return -1;
        }

        TerrainLayer[] terrainLayers = terrainData.terrainLayers ?? Array.Empty<TerrainLayer>();
        for (int i = 0; i < terrainLayers.Length; i++)
        {
            if (terrainLayers[i] == layer)
            {
                return i;
            }
        }

        return -1;
    }

    private static void EnsureLayersExist(Terrain terrain, CompositeTerrainPaintPreset paintPreset)
    {
        if (terrain == null || terrain.terrainData == null || paintPreset == null)
        {
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        List<TerrainLayer> terrainLayers = (terrainData.terrainLayers ?? Array.Empty<TerrainLayer>()).ToList();
        int originalCount = terrainLayers.Count;

        foreach (CompositeTerrainPaintPreset.Entry entry in paintPreset.Entries)
        {
            if (!entry.enabled)
            {
                continue;
            }

            if (entry.layer == null || terrainLayers.Contains(entry.layer))
            {
                continue;
            }

            terrainLayers.Add(entry.layer);
        }

        if (terrainLayers.Count == originalCount)
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Add Terrain Layers");
        terrainData.terrainLayers = terrainLayers.ToArray();
        EditorUtility.SetDirty(terrainData);
        terrain.Flush();
    }

    private static bool WorldToAlphamapCoord(Terrain terrain, Vector3 worldPosition, out Vector2 alphamapCoord)
    {
        alphamapCoord = default;

        if (!WorldToTerrainNormalized(terrain, worldPosition, out Vector2 normalized))
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        alphamapCoord = new Vector2(
            normalized.x * (terrainData.alphamapWidth - 1),
            normalized.y * (terrainData.alphamapHeight - 1));

        return true;
    }

    private static bool WorldToTerrainNormalized(Terrain terrain, Vector3 worldPosition, out Vector2 normalized)
    {
        normalized = default;
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainSize = terrainData.size;
        if (terrainSize.x <= 0f || terrainSize.z <= 0f)
        {
            return false;
        }

        Vector3 localPosition = terrain.transform.InverseTransformPoint(worldPosition);
        normalized = new Vector2(localPosition.x / terrainSize.x, localPosition.z / terrainSize.z);

        return normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f;
    }

    private void PaintAt(
        Terrain terrain,
        CompositeTerrainPaintPreset paintPreset,
        Vector3 worldPosition,
        float brushSize,
        float brushStrength,
        float brushFalloff)
    {
        if (terrain == null || terrain.terrainData == null || paintPreset == null || paintPreset.Entries.Count == 0)
        {
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        int layerCount = terrainData.alphamapLayers;
        if (layerCount <= 0 || terrainData.alphamapWidth <= 0 || terrainData.alphamapHeight <= 0)
        {
            return;
        }

        if (HasMissingPaintableLayers(terrainData, paintPreset))
        {
            return;
        }

        List<PaintEntry> paintEntries = BuildPaintEntries(terrainData, paintPreset);
        if (paintEntries.Count == 0)
        {
            return;
        }

        if (!WorldToAlphamapCoord(terrain, worldPosition, out Vector2 center))
        {
            return;
        }

        float radius = Mathf.Max(MinBrushSize, brushSize) * 0.5f;
        Vector3 worldSize = Vector3.Scale(terrainData.size, terrain.transform.lossyScale);
        float worldWidth = Mathf.Max(0.001f, Mathf.Abs(worldSize.x));
        float worldLength = Mathf.Max(0.001f, Mathf.Abs(worldSize.z));

        int radiusPixelsX = Mathf.CeilToInt(radius / worldWidth * (terrainData.alphamapWidth - 1)) + 1;
        int radiusPixelsY = Mathf.CeilToInt(radius / worldLength * (terrainData.alphamapHeight - 1)) + 1;

        int minX = Mathf.Clamp(Mathf.FloorToInt(center.x) - radiusPixelsX, 0, terrainData.alphamapWidth - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(center.x) + radiusPixelsX, 0, terrainData.alphamapWidth - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(center.y) - radiusPixelsY, 0, terrainData.alphamapHeight - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(center.y) + radiusPixelsY, 0, terrainData.alphamapHeight - 1);

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        float[,,] alphamaps = terrainData.GetAlphamaps(minX, minY, width, height);
        float[] targetWeights = new float[layerCount];
        float[] currentWeights = new float[layerCount];
        float[] blendedWeights = new float[layerCount];
        bool changed = false;

        for (int localY = 0; localY < height; localY++)
        {
            int mapY = minY + localY;
            for (int localX = 0; localX < width; localX++)
            {
                int mapX = minX + localX;
                Vector3 sampleWorld = AlphamapToWorld(terrain, mapX, mapY);
                float distance = HorizontalDistance(worldPosition, sampleWorld);
                if (distance > radius)
                {
                    continue;
                }

                float falloff = ComputeBrushFalloff(distance, radius, brushFalloff);
                float opacity = Mathf.Clamp01(brushStrength * falloff);
                if (opacity <= 0f)
                {
                    continue;
                }

                Array.Clear(targetWeights, 0, targetWeights.Length);
                float effectiveWeightTotal = 0f;
                foreach (PaintEntry entry in paintEntries)
                {
                    float entryMask = ComputeEntryMask(entry, sampleWorld);
                    float effectiveWeight = entry.Weight * entryMask;
                    if (effectiveWeight <= 0f)
                    {
                        continue;
                    }

                    targetWeights[entry.LayerIndex] += effectiveWeight;
                    effectiveWeightTotal += effectiveWeight;
                }

                if (effectiveWeightTotal <= 0f)
                {
                    continue;
                }

                for (int layer = 0; layer < layerCount; layer++)
                {
                    targetWeights[layer] /= effectiveWeightTotal;
                    currentWeights[layer] = alphamaps[localY, localX, layer];
                }

                CompositeTerrainPaintMath.BlendNormalized(
                    currentWeights,
                    targetWeights,
                    opacity,
                    blendedWeights);
                for (int layer = 0; layer < layerCount; layer++)
                {
                    alphamaps[localY, localX, layer] = blendedWeights[layer];
                }

                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        RegisterTerrainUndo(terrain);
        terrainData.SetAlphamaps(minX, minY, alphamaps);
        EditorUtility.SetDirty(terrainData);
        terrain.Flush();
        SceneView.RepaintAll();
    }

    private static List<PaintEntry> BuildPaintEntries(TerrainData terrainData, CompositeTerrainPaintPreset paintPreset)
    {
        List<PaintEntry> paintEntries = new();
        foreach (CompositeTerrainPaintPreset.Entry entry in paintPreset.Entries)
        {
            if (!entry.enabled || entry.layer == null || entry.weight <= 0f || entry.coverage <= 0f)
            {
                continue;
            }

            int layerIndex = GetLayerIndex(terrainData, entry.layer);
            if (layerIndex < 0 || layerIndex >= terrainData.alphamapLayers)
            {
                continue;
            }

            paintEntries.Add(new PaintEntry(
                layerIndex,
                Mathf.Max(0f, entry.weight),
                Mathf.Clamp01(entry.coverage),
                Mathf.Max(0.001f, entry.noiseScale),
                Mathf.Clamp01(entry.noiseInfluence),
                entry.seed));
        }

        return paintEntries;
    }

    private static bool HasMissingPaintableLayers(TerrainData terrainData, CompositeTerrainPaintPreset paintPreset)
    {
        foreach (CompositeTerrainPaintPreset.Entry entry in paintPreset.Entries)
        {
            if (!entry.enabled || entry.layer == null || entry.weight <= 0f || entry.coverage <= 0f)
            {
                continue;
            }

            int layerIndex = GetLayerIndex(terrainData, entry.layer);
            if (layerIndex < 0 || layerIndex >= terrainData.alphamapLayers)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 AlphamapToWorld(Terrain terrain, int alphamapX, int alphamapY)
    {
        TerrainData terrainData = terrain.terrainData;
        float normalizedX = terrainData.alphamapWidth > 1
            ? alphamapX / (float)(terrainData.alphamapWidth - 1)
            : 0f;
        float normalizedY = terrainData.alphamapHeight > 1
            ? alphamapY / (float)(terrainData.alphamapHeight - 1)
            : 0f;

        Vector3 localPosition = new(
            normalizedX * terrainData.size.x,
            terrainData.GetInterpolatedHeight(normalizedX, normalizedY),
            normalizedY * terrainData.size.z);

        return terrain.transform.TransformPoint(localPosition);
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    private static float ComputeBrushFalloff(float distance, float radius, float falloff)
    {
        if (radius <= 0f || distance > radius)
        {
            return 0f;
        }

        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float clampedFalloff = Mathf.Clamp01(falloff);
        if (clampedFalloff <= 0.001f)
        {
            return 1f;
        }

        float innerDistance = 1f - clampedFalloff;
        if (normalizedDistance <= innerDistance)
        {
            return 1f;
        }

        float edgeT = 1f - Mathf.InverseLerp(innerDistance, 1f, normalizedDistance);
        return Mathf.SmoothStep(0f, 1f, edgeT);
    }

    private static float ComputeEntryMask(PaintEntry entry, Vector3 worldPosition)
    {
        if (entry.Coverage <= 0f)
        {
            return 0f;
        }

        if (entry.Coverage >= 1f && entry.NoiseInfluence <= 0f)
        {
            return 1f;
        }

        float noise = ComputeNoise(worldPosition, entry.NoiseScale, entry.Seed);
        float patchMask = entry.Coverage >= 1f
            ? 1f
            : 1f - Mathf.SmoothStep(
                Mathf.Clamp01(entry.Coverage - CoverageThresholdWidth),
                Mathf.Clamp01(entry.Coverage + CoverageThresholdWidth),
                noise);

        float coverageMask = Mathf.Lerp(entry.Coverage, patchMask, entry.NoiseInfluence);
        if (entry.Coverage >= 1f)
        {
            float modulation = Mathf.Lerp(1f, Mathf.Lerp(0.75f, 1.25f, noise), entry.NoiseInfluence);
            coverageMask *= modulation;
        }

        return Mathf.Clamp01(coverageMask);
    }

    private static float ComputeNoise(Vector3 worldPosition, float noiseScale, int seed)
    {
        float scale = Mathf.Max(0.001f, noiseScale);
        float offsetX = seed * 37.719f + 19.19f;
        float offsetY = seed * 11.131f - 73.73f;

        return Mathf.PerlinNoise(
            worldPosition.x / scale + offsetX,
            worldPosition.z / scale + offsetY);
    }

    private readonly struct PaintEntry
    {
        public PaintEntry(
            int layerIndex,
            float weight,
            float coverage,
            float noiseScale,
            float noiseInfluence,
            int seed)
        {
            LayerIndex = layerIndex;
            Weight = weight;
            Coverage = coverage;
            NoiseScale = noiseScale;
            NoiseInfluence = noiseInfluence;
            Seed = seed;
        }

        public int LayerIndex { get; }
        public float Weight { get; }
        public float Coverage { get; }
        public float NoiseScale { get; }
        public float NoiseInfluence { get; }
        public int Seed { get; }
    }
}
}
#endif
