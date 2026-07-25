#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

public sealed class DetailTerrainPainterWindow : EditorWindow
{
    private const string MenuPath = "Tools/Terrain Tools/Painters/Detail Preset Painter";
    private const string UndoName = "Detail Terrain Paint";
    private const float MinBrushSize = 0.25f;
    private const float MaxBrushSize = 256f;
    private const float BrushPreviewOffset = 0.04f;

    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private DetailTerrainPaintPreset preset;
    [SerializeField] private bool paintingEnabled = true;
    [SerializeField] private float targetDensity = 0.6f;
    [SerializeField] private float brushSize = 8f;
    [SerializeField] private float brushStrength = 0.35f;
    [SerializeField] private float brushFalloff = 0.75f;

    private SerializedObject presetSerializedObject;
    private SerializedProperty entriesProperty;
    private ReorderableList entriesList;
    private bool isPainting;
    private readonly TerrainPaintUndoTransaction undoTransaction = new();

    private float BrushRadius => Mathf.Max(MinBrushSize, brushSize) * 0.5f;

    [MenuItem(MenuPath, priority = 2411)]
    private static void Open()
    {
        DetailTerrainPainterWindow window = GetWindow<DetailTerrainPainterWindow>("Detail Preset Painter");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
        TryUseSelectedTerrain();
        RebuildPresetSerializedObject();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
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
        DetailTerrainPaintPreset newPreset = (DetailTerrainPaintPreset)EditorGUILayout.ObjectField(
            "Preset",
            preset,
            typeof(DetailTerrainPaintPreset),
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
        targetDensity = EditorGUILayout.Slider("Target Density", targetDensity, 0f, 1f);
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

        using (new EditorGUI.DisabledScope(preset == null || targetTerrain == null || targetTerrain.terrainData == null))
        {
            if (GUILayout.Button("Add All Detail Prototypes From Terrain"))
            {
                AddAllDetailPrototypesFromTerrain();
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

        if (terrainData.detailWidth <= 0 || terrainData.detailHeight <= 0)
        {
            EditorGUILayout.HelpBox("The selected TerrainData has no detail resolution.", MessageType.Warning);
            return;
        }

        if (terrainData.detailPrototypes == null || terrainData.detailPrototypes.Length == 0)
        {
            EditorGUILayout.HelpBox("The selected TerrainData has no Detail Prototypes assigned.", MessageType.Warning);
            return;
        }

        if (preset == null)
        {
            EditorGUILayout.HelpBox("Create or assign a DetailTerrainPaintPreset.", MessageType.Info);
            return;
        }

        if (preset.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox("The preset has no detail entries.", MessageType.Info);
            return;
        }

        if (!DetailTerrainPaintUtility.ValidatePreset(terrainData, preset, out string error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox("Left mouse paints. Hold Shift while painting to erase this preset's detail layers.", MessageType.None);
    }

    private void DrawPresetEntries()
    {
        EditorGUILayout.LabelField("Detail Entries", EditorStyles.boldLabel);

        if (preset == null || presetSerializedObject == null || entriesProperty == null || entriesList == null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button("Add Detail Entry");
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

    private void DrawDetailEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        SerializedProperty enabled = entry.FindPropertyRelative("enabled");
        SerializedProperty prefab = entry.FindPropertyRelative("prefab");
        SerializedProperty texture = entry.FindPropertyRelative("texture");
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
            DrawDetailPrototypeSelector(selectorRect, index, prefab, texture);

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

        if (current == null || current.alt)
        {
            if (current != null && current.rawType == EventType.MouseUp)
            {
                EndPaintStroke();
            }

            return;
        }

        if (!TryGetTerrainHit(current.mousePosition, out Terrain hitTerrain, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            if (current.rawType == EventType.MouseUp)
            {
                EndPaintStroke();
            }

            return;
        }

        if (hitTerrain != targetTerrain)
        {
            targetTerrain = hitTerrain;
            Repaint();
        }

        if (!DetailTerrainPaintUtility.ValidatePreset(hitTerrain.terrainData, preset, out _))
        {
            return;
        }

        DrawBrushPreview(hitPoint, hitNormal, current.shift);
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
            BeginPaintStroke(hitTerrain);
            PaintDab(hitTerrain, hitPoint, current.shift);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && isPainting)
        {
            RegisterTerrainUndo(hitTerrain);
            PaintDab(hitTerrain, hitPoint, current.shift);
            current.Use();
        }
        else if (current.rawType == EventType.MouseUp)
        {
            EndPaintStroke();
        }
    }

    private void PaintDab(Terrain terrain, Vector3 hitPoint, bool eraseMode)
    {
        try
        {
            DetailTerrainPaintUtility.PaintAt(
                terrain,
                preset,
                hitPoint,
                brushSize,
                brushStrength,
                brushFalloff,
                targetDensity,
                eraseMode);
        }
        catch (Exception exception)
        {
            AbortPaintStroke(exception);
        }
    }

    private void BeginPaintStroke(Terrain terrain)
    {
        if (isPainting || terrain == null || terrain.terrainData == null)
        {
            return;
        }

        undoTransaction.Begin(UndoName);
        RegisterTerrainUndo(terrain);
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

    private void DrawBrushPreview(Vector3 hitPoint, Vector3 hitNormal, bool eraseMode)
    {
        float radius = BrushRadius;
        Vector3 normal = hitNormal.sqrMagnitude > 0f ? hitNormal.normalized : Vector3.up;
        Vector3 center = hitPoint + normal * BrushPreviewOffset;

        Color fillColor = eraseMode ? new Color(1f, 0.35f, 0.15f, 0.12f) : new Color(0.25f, 0.85f, 0.35f, 0.12f);
        Color outlineColor = eraseMode ? new Color(1f, 0.35f, 0.15f, 0.9f) : new Color(0.25f, 0.9f, 0.35f, 0.9f);

        Color previous = Handles.color;
        Handles.color = fillColor;
        Handles.DrawSolidDisc(center, normal, radius);
        Handles.color = outlineColor;
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
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Detail Entries"),
            drawElementCallback = DrawDetailEntry,
            elementHeightCallback = _ =>
            {
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                return (lineHeight * 6f) + TerrainPrototypePicker.SelectorHeight + (spacing * 8f);
            },
            onAddCallback = _ => ShowDetailPicker(TerrainPrototypePicker.GetCurrentEventAnchor(), -1),
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
            "Create Detail Terrain Paint Preset",
            "DetailTerrainPaintPreset",
            "asset",
            "Choose where to save the new terrain detail paint preset.");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        DetailTerrainPaintPreset newPreset = CreateInstance<DetailTerrainPaintPreset>();
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
        DetailTerrainPaintPreset selectedPreset = Selection.activeObject as DetailTerrainPaintPreset;
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
            "Select a DetailTerrainPaintPreset asset in the Project window, then click Load/Use Preset.",
            "OK");
    }

    private void DrawDetailPrototypeSelector(
        Rect rect,
        int entryIndex,
        SerializedProperty prefabProperty,
        SerializedProperty textureProperty)
    {
        GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
        Texture2D texture = textureProperty.objectReferenceValue as Texture2D;
        UnityEngine.Object source = prefab != null ? prefab : texture;
        bool isRegistered = TerrainPrototypeResolver.TryResolveDetail(
            targetTerrain != null ? targetTerrain.terrainData : null,
            prefab,
            texture,
            out _,
            out string error);
        string displayName = source != null ? source.name : string.Empty;
        if (source != null && !isRegistered)
        {
            displayName += " (not uniquely available)";
        }

        if (TerrainPrototypePicker.DrawSelector(
                rect,
                new GUIContent("Detail Prototype"),
                displayName,
                source,
                texture,
                source != null,
                isRegistered ? "Choose one of the detail prototypes registered on the selected Terrain." : error))
        {
            ShowDetailPicker(rect, entryIndex);
        }
    }

    private void ShowDetailPicker(Rect anchorRect, int entryIndex)
    {
        TerrainData terrainData = targetTerrain != null ? targetTerrain.terrainData : null;
        DetailPrototype[] prototypes = terrainData != null ? terrainData.detailPrototypes : null;
        prototypes ??= Array.Empty<DetailPrototype>();

        HashSet<UnityEngine.Object> usedSources = new();
        UnityEngine.Object selectedSource = null;
        if (entriesProperty != null)
        {
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
                UnityEngine.Object source = entry.FindPropertyRelative("prefab").objectReferenceValue
                    ?? entry.FindPropertyRelative("texture").objectReferenceValue;
                if (i == entryIndex)
                {
                    selectedSource = source;
                }
                else if (source != null)
                {
                    usedSources.Add(source);
                }
            }
        }

        List<TerrainPrototypePickerOption> options = new(prototypes.Length);
        int selectedIndex = -1;
        for (int i = 0; i < prototypes.Length; i++)
        {
            UnityEngine.Object source = GetPrototypeSource(prototypes[i]);
            int occurrenceCount = CountDetailPrototypeOccurrences(prototypes, source);
            bool alreadyUsed = source != null && usedSources.Contains(source);
            bool ambiguous = source != null && occurrenceCount > 1;
            bool enabled = source != null && !alreadyUsed && !ambiguous;
            if (source == selectedSource && selectedIndex < 0)
            {
                selectedIndex = i;
            }

            string disabledReason = source == null
                ? "This detail prototype has no prefab or texture."
                : ambiguous
                    ? "This source occurs more than once on the Terrain. Remove duplicate prototypes first."
                    : alreadyUsed
                        ? "Already used by this preset."
                        : null;
            options.Add(new TerrainPrototypePickerOption(
                i,
                source != null ? source.name : $"Missing Detail {i + 1}",
                $"Detail {i + 1}",
                source,
                source as Texture,
                enabled,
                disabledReason));
        }

        TerrainPrototypePicker.Show(
            anchorRect,
            "Select Detail Prototype",
            targetTerrain != null ? targetTerrain.name : null,
            options,
            selectedIndex,
            prototypeIndex =>
            {
                if (entryIndex < 0)
                {
                    AddDetailEntry(prototypeIndex);
                }
                else
                {
                    AssignDetailPrototypeToEntry(entryIndex, prototypeIndex);
                }
            });
    }

    private void AddDetailEntry(int prototypeIndex)
    {
        if (!TryGetDetailPrototypeSource(prototypeIndex, out UnityEngine.Object source))
        {
            return;
        }

        presetSerializedObject.Update();
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);
        SetDetailEntryDefaults(entriesProperty.GetArrayElementAtIndex(index), source);
        ApplyPresetPickerChange();
    }

    private void AssignDetailPrototypeToEntry(int entryIndex, int prototypeIndex)
    {
        if (entriesProperty == null
            || entryIndex < 0
            || entryIndex >= entriesProperty.arraySize
            || !TryGetDetailPrototypeSource(prototypeIndex, out UnityEngine.Object source))
        {
            return;
        }

        presetSerializedObject.Update();
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(entryIndex);
        entry.FindPropertyRelative("prefab").objectReferenceValue = source as GameObject;
        entry.FindPropertyRelative("texture").objectReferenceValue = source as Texture2D;
        ApplyPresetPickerChange();
    }

    private bool TryGetDetailPrototypeSource(int prototypeIndex, out UnityEngine.Object source)
    {
        TerrainData terrainData = targetTerrain != null ? targetTerrain.terrainData : null;
        DetailPrototype[] prototypes = terrainData != null ? terrainData.detailPrototypes : null;
        source = null;
        if (prototypes == null || prototypeIndex < 0 || prototypeIndex >= prototypes.Length)
        {
            return false;
        }

        source = GetPrototypeSource(prototypes[prototypeIndex]);
        return source != null && CountDetailPrototypeOccurrences(prototypes, source) == 1;
    }

    private static void SetDetailEntryDefaults(SerializedProperty entry, UnityEngine.Object source)
    {
        entry.FindPropertyRelative("enabled").boolValue = true;
        entry.FindPropertyRelative("prefab").objectReferenceValue = source as GameObject;
        entry.FindPropertyRelative("texture").objectReferenceValue = source as Texture2D;
        entry.FindPropertyRelative("weight").floatValue = 1f;
        entry.FindPropertyRelative("coverage").floatValue = 1f;
        entry.FindPropertyRelative("noiseScale").floatValue = 12f;
        entry.FindPropertyRelative("noiseInfluence").floatValue = 1f;
        entry.FindPropertyRelative("seed").intValue = 0;
    }

    private void ApplyPresetPickerChange()
    {
        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        RebuildPresetSerializedObject();
        Repaint();
        SceneView.RepaintAll();
    }

    private static int CountDetailPrototypeOccurrences(
        IReadOnlyList<DetailPrototype> prototypes,
        UnityEngine.Object source)
    {
        if (source == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < prototypes.Count; i++)
        {
            if (GetPrototypeSource(prototypes[i]) == source)
            {
                count++;
            }
        }

        return count;
    }

    private void AddAllDetailPrototypesFromTerrain()
    {
        if (preset == null || targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        DetailPrototype[] prototypes = targetTerrain.terrainData.detailPrototypes;
        int prototypeCount = prototypes != null ? prototypes.Length : 0;
        if (prototypeCount <= 0)
        {
            return;
        }

        if (presetSerializedObject == null)
        {
            RebuildPresetSerializedObject();
        }

        presetSerializedObject.Update();
        entriesProperty.ClearArray();

        for (int i = 0; i < prototypeCount; i++)
        {
            entriesProperty.InsertArrayElementAtIndex(i);
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("enabled").boolValue = true;
            UnityEngine.Object source = GetPrototypeSource(prototypes[i]);
            entry.FindPropertyRelative("prefab").objectReferenceValue = source as GameObject;
            entry.FindPropertyRelative("texture").objectReferenceValue = source as Texture2D;
            entry.FindPropertyRelative("weight").floatValue = 1f;
            entry.FindPropertyRelative("coverage").floatValue = 1f;
            entry.FindPropertyRelative("noiseScale").floatValue = 12f;
            entry.FindPropertyRelative("noiseInfluence").floatValue = 1f;
            entry.FindPropertyRelative("seed").intValue = i;
        }

        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        RebuildPresetSerializedObject();
        SceneView.RepaintAll();
    }

    private static bool TryGetTerrainHit(
        Vector2 guiMousePosition,
        out Terrain terrain,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        terrain = null;
        hitPoint = default;
        hitNormal = Vector3.up;

        Ray ray = HandleUtility.GUIPointToWorldRay(guiMousePosition);
        RaycastHit closestHit = default;
        Terrain closestTerrain = null;
        float closestDistance = float.MaxValue;

        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain candidateTerrain = terrains[i];
            TerrainData terrainData = candidateTerrain != null ? candidateTerrain.terrainData : null;
            TerrainCollider terrainCollider = candidateTerrain != null ? candidateTerrain.GetComponent<TerrainCollider>() : null;
            if (terrainData == null || terrainCollider == null)
            {
                continue;
            }

            if (!terrainCollider.Raycast(ray, out RaycastHit hit, float.MaxValue) || hit.distance >= closestDistance)
            {
                continue;
            }

            closestHit = hit;
            closestTerrain = candidateTerrain;
            closestDistance = hit.distance;
        }

        if (closestTerrain == null)
        {
            return false;
        }

        terrain = closestTerrain;
        TerrainData closestTerrainData = closestTerrain.terrainData;
        hitPoint = closestHit.point;
        if (WorldToTerrainNormalized(terrain, hitPoint, out Vector2 normalized))
        {
            hitNormal = terrain.transform.TransformDirection(
                closestTerrainData.GetInterpolatedNormal(normalized.x, normalized.y)).normalized;
        }
        else
        {
            hitNormal = closestHit.normal;
        }

        return true;
    }

    private static bool WorldToDetailCoord(Terrain terrain, Vector3 worldPosition, out Vector2 detailCoord)
    {
        detailCoord = default;

        if (!WorldToTerrainNormalized(terrain, worldPosition, out Vector2 normalized))
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        detailCoord = new Vector2(
            normalized.x * (terrainData.detailWidth - 1),
            normalized.y * (terrainData.detailHeight - 1));

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

    private static UnityEngine.Object GetPrototypeSource(DetailPrototype prototype)
    {
        if (prototype.prototype != null)
        {
            return prototype.prototype;
        }

        return prototype.prototypeTexture;
    }

}
}
#endif
