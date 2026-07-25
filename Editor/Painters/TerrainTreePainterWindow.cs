#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

public sealed class TerrainTreePainterWindow : EditorWindow
{
    private const string MenuPath = "Tools/Terrain Tools/Painters/Tree Preset Painter";
    private const string UndoName = "Terrain Tree Paint";
    private const float MinBrushSize = 0.25f;
    private const float MaxBrushSize = 256f;
    private const float BrushPreviewOffset = 0.04f;

    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private TerrainTreePaintPreset preset;
    [SerializeField] private bool paintingEnabled = true;
    [SerializeField] private float treesPer100SquareMeters = 4f;
    [SerializeField] private float minimumSpacing = 2f;
    [SerializeField] private float brushSize = 20f;
    [SerializeField] private float brushStrength = 0.5f;
    [SerializeField] private float brushFalloff = 0.75f;

    private SerializedObject presetSerializedObject;
    private SerializedProperty entriesProperty;
    private ReorderableList entriesList;
    private Vector2 scrollPosition;
    private bool isPainting;
    private int paintDabIndex;
    private Vector3 lastPaintPosition;
    private bool hasLastPaintPosition;
    private readonly TerrainPaintUndoTransaction undoTransaction = new();
    private TerrainTreePaintStrokeContext strokeContext;

    private float BrushRadius => Mathf.Max(MinBrushSize, brushSize) * 0.5f;

    [MenuItem(MenuPath, priority = 2412)]
    private static void Open()
    {
        TerrainTreePainterWindow window = GetWindow<TerrainTreePainterWindow>("Tree Preset Painter");
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
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUI.BeginChangeCheck();
        Terrain newTarget = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck())
        {
            targetTerrain = newTarget;
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();
        TerrainTreePaintPreset newPreset = (TerrainTreePaintPreset)EditorGUILayout.ObjectField(
            "Preset",
            preset,
            typeof(TerrainTreePaintPreset),
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
        treesPer100SquareMeters = Mathf.Max(0f, EditorGUILayout.FloatField("Trees per 100 m²", treesPer100SquareMeters));
        minimumSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Minimum Spacing", minimumSpacing));
        brushSize = EditorGUILayout.Slider("Brush Size", brushSize, MinBrushSize, MaxBrushSize);
        brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);
        brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0f, 1f);

        EditorGUILayout.Space(8f);
        DrawValidationMessages();

        EditorGUILayout.Space(8f);
        DrawPresetEntries();
        EditorGUILayout.EndScrollView();
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
            if (GUILayout.Button("Add All Tree Prototypes From Terrain"))
            {
                AddAllTreePrototypesFromTerrain();
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

        if (terrainData.treePrototypes == null || terrainData.treePrototypes.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "The selected TerrainData has no Tree Prototypes. Add them through Terrain > Paint Trees first.",
                MessageType.Warning);
            return;
        }

        if (preset == null)
        {
            EditorGUILayout.HelpBox("Create or assign a TerrainTreePaintPreset.", MessageType.Info);
            return;
        }

        if (preset.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox("The preset has no tree entries.", MessageType.Info);
            return;
        }

        if (!TerrainTreePaintUtility.ValidatePreset(terrainData, preset, out string error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            "Left mouse paints native Terrain TreeInstances. Hold Shift while painting to erase only tree types in this preset.",
            MessageType.None);
    }

    private void DrawPresetEntries()
    {
        EditorGUILayout.LabelField("Tree Entries", EditorStyles.boldLabel);

        if (preset == null || presetSerializedObject == null || entriesProperty == null || entriesList == null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button("Add Tree Entry");
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

    private void DrawTreeEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        SerializedProperty enabled = entry.FindPropertyRelative("enabled");
        SerializedProperty prefab = entry.FindPropertyRelative("prefab");
        SerializedProperty weight = entry.FindPropertyRelative("weight");
        SerializedProperty randomRotation = entry.FindPropertyRelative("randomRotation");
        SerializedProperty minHeightScale = entry.FindPropertyRelative("minHeightScale");
        SerializedProperty maxHeightScale = entry.FindPropertyRelative("maxHeightScale");
        SerializedProperty lockWidthToHeight = entry.FindPropertyRelative("lockWidthToHeight");
        SerializedProperty minWidthScale = entry.FindPropertyRelative("minWidthScale");
        SerializedProperty maxWidthScale = entry.FindPropertyRelative("maxWidthScale");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect line = new(rect.x, rect.y + spacing, rect.width, lineHeight);

        EditorGUI.LabelField(line, $"Entry {index + 1}", EditorStyles.boldLabel);
        Rect enabledRect = new(rect.x + rect.width - 90f, line.y, 90f, lineHeight);
        EditorGUI.PropertyField(enabledRect, enabled, new GUIContent("Enabled"));

        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            line.y += lineHeight + spacing;
            Rect selectorRect = new(line.x, line.y, line.width, TerrainPrototypePicker.SelectorHeight);
            DrawTreePrototypeSelector(selectorRect, index, prefab);

            line.y += TerrainPrototypePicker.SelectorHeight + spacing;
            weight.floatValue = Mathf.Max(0f, EditorGUI.FloatField(line, "Proportion Weight", weight.floatValue));

            line.y += lineHeight + spacing;
            EditorGUI.LabelField(line, "Normalized Proportion", GetNormalizedProportion(index).ToString("P1"));

            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, randomRotation, new GUIContent("Random Rotation"));

            line.y += lineHeight + spacing;
            DrawPositiveRange(line, "Height Scale", minHeightScale, maxHeightScale);

            line.y += lineHeight + spacing;
            EditorGUI.PropertyField(line, lockWidthToHeight, new GUIContent("Lock Width To Height"));

            if (!lockWidthToHeight.boolValue)
            {
                line.y += lineHeight + spacing;
                DrawPositiveRange(line, "Width Scale", minWidthScale, maxWidthScale);
            }
        }
    }

    private static void DrawPositiveRange(
        Rect rect,
        string label,
        SerializedProperty minimumProperty,
        SerializedProperty maximumProperty)
    {
        Rect valuesRect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
        const float groupSpacing = 6f;
        float groupWidth = Mathf.Max(1f, (valuesRect.width - groupSpacing) * 0.5f);
        Rect minimumRect = new(valuesRect.x, valuesRect.y, groupWidth, valuesRect.height);
        Rect maximumRect = new(minimumRect.xMax + groupSpacing, valuesRect.y, groupWidth, valuesRect.height);

        float minimum = DrawCompactFloatField(minimumRect, "Min", minimumProperty.floatValue);
        float maximum = DrawCompactFloatField(maximumRect, "Max", maximumProperty.floatValue);
        minimumProperty.floatValue = Mathf.Max(0.01f, minimum);
        maximumProperty.floatValue = Mathf.Max(minimumProperty.floatValue, maximum);
    }

    private static float DrawCompactFloatField(Rect rect, string label, float value)
    {
        float labelWidth = Mathf.Clamp(rect.width * 0.28f, 20f, 30f);
        Rect labelRect = new(rect.x, rect.y, labelWidth, rect.height);
        Rect fieldRect = new(
            labelRect.xMax + 2f,
            rect.y,
            Mathf.Max(1f, rect.width - labelWidth - 2f),
            rect.height);
        EditorGUI.LabelField(labelRect, label, EditorStyles.miniLabel);
        return EditorGUI.FloatField(fieldRect, value);
    }

    private float GetNormalizedProportion(int index)
    {
        float total = 0f;
        float selectedWeight = 0f;
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty candidate = entriesProperty.GetArrayElementAtIndex(i);
            if (!candidate.FindPropertyRelative("enabled").boolValue)
            {
                continue;
            }

            float candidateWeight = Mathf.Max(0f, candidate.FindPropertyRelative("weight").floatValue);
            total += candidateWeight;
            if (i == index)
            {
                selectedWeight = candidateWeight;
            }
        }

        return total > 0f ? selectedWeight / total : 0f;
    }

    private void DuringSceneGui(SceneView sceneView)
    {
        Event current = Event.current;
        if (!paintingEnabled || current == null || current.alt)
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

        if (!TerrainTreePaintUtility.ValidatePreset(hitTerrain.terrainData, preset, out _))
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
            PaintDab(hitTerrain, hitPoint, current.shift, true);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && isPainting)
        {
            PaintDab(hitTerrain, hitPoint, current.shift, false);
            current.Use();
        }
        else if (current.rawType == EventType.MouseUp)
        {
            EndPaintStroke();
        }
    }

    private void PaintDab(Terrain terrain, Vector3 hitPoint, bool eraseMode, bool force)
    {
        float minimumDabDistance = Mathf.Max(0.25f, brushSize * 0.1f);
        if (!force && hasLastPaintPosition && HorizontalDistance(lastPaintPosition, hitPoint) < minimumDabDistance)
        {
            return;
        }

        RegisterTerrainUndo(terrain);
        int randomSeed = unchecked(Environment.TickCount * 397) ^ paintDabIndex++;
        try
        {
            TerrainTreePaintUtility.PaintAt(
                terrain,
                preset,
                hitPoint,
                brushSize,
                brushStrength,
                brushFalloff,
                treesPer100SquareMeters,
                minimumSpacing,
                eraseMode,
                randomSeed,
                strokeContext);
        }
        catch (Exception exception)
        {
            AbortPaintStroke(exception);
            return;
        }

        lastPaintPosition = hitPoint;
        hasLastPaintPosition = true;
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
        paintDabIndex = 0;
        hasLastPaintPosition = false;
        strokeContext = new TerrainTreePaintStrokeContext();
    }

    private void EndPaintStroke()
    {
        if (!isPainting)
        {
            return;
        }

        undoTransaction.Complete();
        isPainting = false;
        hasLastPaintPosition = false;
        strokeContext = null;
    }

    private void AbortPaintStroke(Exception exception)
    {
        undoTransaction.Revert();
        isPainting = false;
        hasLastPaintPosition = false;
        strokeContext = null;
        Debug.LogException(exception);
    }

    private void RegisterTerrainUndo(Terrain terrain)
    {
        TerrainData terrainData = terrain != null ? terrain.terrainData : null;
        undoTransaction.Register(terrainData);
    }

    private void DrawBrushPreview(Vector3 hitPoint, Vector3 hitNormal, bool eraseMode)
    {
        Vector3 normal = hitNormal.sqrMagnitude > 0f ? hitNormal.normalized : Vector3.up;
        Vector3 center = hitPoint + normal * BrushPreviewOffset;
        Color fillColor = eraseMode ? new Color(1f, 0.35f, 0.15f, 0.12f) : new Color(0.25f, 0.85f, 0.35f, 0.12f);
        Color outlineColor = eraseMode ? new Color(1f, 0.35f, 0.15f, 0.9f) : new Color(0.25f, 0.9f, 0.35f, 0.9f);

        Color previous = Handles.color;
        Handles.color = fillColor;
        Handles.DrawSolidDisc(center, normal, BrushRadius);
        Handles.color = outlineColor;
        Handles.DrawWireDisc(center, normal, BrushRadius);
        Handles.color = previous;
    }

    private void TryUseSelectedTerrain()
    {
        Terrain selectedTerrain = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Terrain>()
            : null;
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
        return new ReorderableList(presetSerializedObject, entriesProperty, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Tree Entries"),
            drawElementCallback = DrawTreeEntry,
            elementHeightCallback = index =>
            {
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
                bool locked = entry.FindPropertyRelative("lockWidthToHeight").boolValue;
                int lineCount = locked ? 7 : 8;
                int regularLineCount = lineCount - 1;
                return (EditorGUIUtility.singleLineHeight * regularLineCount)
                    + TerrainPrototypePicker.SelectorHeight
                    + (EditorGUIUtility.standardVerticalSpacing * (lineCount + 1));
            },
            onAddCallback = _ => ShowTreePicker(TerrainPrototypePicker.GetCurrentEventAnchor(), -1),
            onRemoveCallback = list =>
            {
                if (list.index >= 0 && list.index < entriesProperty.arraySize)
                {
                    entriesProperty.DeleteArrayElementAtIndex(list.index);
                }
            }
        };
    }

    private void CreateNewPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Terrain Tree Paint Preset",
            "TerrainTreePaintPreset",
            "asset",
            "Choose where to save the new terrain tree paint preset.");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        TerrainTreePaintPreset newPreset = CreateInstance<TerrainTreePaintPreset>();
        AssetDatabase.CreateAsset(newPreset, path);
        AssetDatabase.SaveAssets();
        preset = newPreset;
        RebuildPresetSerializedObject();
        EditorGUIUtility.PingObject(preset);
    }

    private void SavePreset()
    {
        presetSerializedObject?.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
    }

    private void LoadSelectedPreset()
    {
        if (Selection.activeObject is TerrainTreePaintPreset selectedPreset)
        {
            preset = selectedPreset;
            RebuildPresetSerializedObject();
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        EditorUtility.DisplayDialog(
            "Load Preset",
            "Select a TerrainTreePaintPreset asset in the Project window, then click Load/Use Preset.",
            "OK");
    }

    private void DrawTreePrototypeSelector(Rect rect, int entryIndex, SerializedProperty prefabProperty)
    {
        GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
        bool isRegistered = TerrainPrototypeResolver.TryResolveTree(
            targetTerrain != null ? targetTerrain.terrainData : null,
            prefab,
            out _,
            out string error);
        string displayName = prefab != null ? prefab.name : string.Empty;
        if (prefab != null && !isRegistered)
        {
            displayName += " (not uniquely available)";
        }

        if (TerrainPrototypePicker.DrawSelector(
                rect,
                new GUIContent("Tree Prototype"),
                displayName,
                prefab,
                null,
                prefab != null,
                isRegistered ? "Choose one of the tree prototypes registered on the selected Terrain." : error))
        {
            ShowTreePicker(rect, entryIndex);
        }
    }

    private void ShowTreePicker(Rect anchorRect, int entryIndex)
    {
        TerrainData terrainData = targetTerrain != null ? targetTerrain.terrainData : null;
        TreePrototype[] prototypes = terrainData != null ? terrainData.treePrototypes : null;
        prototypes ??= Array.Empty<TreePrototype>();

        HashSet<GameObject> usedPrefabs = new();
        GameObject selectedPrefab = null;
        if (entriesProperty != null)
        {
            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                GameObject prefab = entriesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("prefab")
                    .objectReferenceValue as GameObject;
                if (i == entryIndex)
                {
                    selectedPrefab = prefab;
                }
                else if (prefab != null)
                {
                    usedPrefabs.Add(prefab);
                }
            }
        }

        List<TerrainPrototypePickerOption> options = new(prototypes.Length);
        int selectedIndex = -1;
        for (int i = 0; i < prototypes.Length; i++)
        {
            GameObject prefab = prototypes[i].prefab;
            int occurrenceCount = CountTreePrototypeOccurrences(prototypes, prefab);
            bool alreadyUsed = prefab != null && usedPrefabs.Contains(prefab);
            bool ambiguous = prefab != null && occurrenceCount > 1;
            bool enabled = prefab != null && !alreadyUsed && !ambiguous;
            if (prefab == selectedPrefab && selectedIndex < 0)
            {
                selectedIndex = i;
            }

            string disabledReason = prefab == null
                ? "This tree prototype has no prefab."
                : ambiguous
                    ? "This prefab occurs more than once on the Terrain. Remove duplicate prototypes first."
                    : alreadyUsed
                        ? "Already used by this preset."
                        : null;
            options.Add(new TerrainPrototypePickerOption(
                i,
                prefab != null ? prefab.name : $"Missing Tree {i + 1}",
                $"Tree {i + 1}",
                prefab,
                null,
                enabled,
                disabledReason));
        }

        TerrainPrototypePicker.Show(
            anchorRect,
            "Select Tree Prototype",
            targetTerrain != null ? targetTerrain.name : null,
            options,
            selectedIndex,
            prototypeIndex =>
            {
                if (entryIndex < 0)
                {
                    AddTreeEntry(prototypeIndex);
                }
                else
                {
                    AssignTreePrototypeToEntry(entryIndex, prototypeIndex);
                }
            });
    }

    private void AddTreeEntry(int prototypeIndex)
    {
        if (!TryGetTreePrototypePrefab(prototypeIndex, out GameObject prefab))
        {
            return;
        }

        presetSerializedObject.Update();
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);
        SetEntryDefaults(entriesProperty.GetArrayElementAtIndex(index), prefab);
        ApplyPresetPickerChange();
    }

    private void AssignTreePrototypeToEntry(int entryIndex, int prototypeIndex)
    {
        if (entriesProperty == null
            || entryIndex < 0
            || entryIndex >= entriesProperty.arraySize
            || !TryGetTreePrototypePrefab(prototypeIndex, out GameObject prefab))
        {
            return;
        }

        presetSerializedObject.Update();
        entriesProperty
            .GetArrayElementAtIndex(entryIndex)
            .FindPropertyRelative("prefab")
            .objectReferenceValue = prefab;
        ApplyPresetPickerChange();
    }

    private bool TryGetTreePrototypePrefab(int prototypeIndex, out GameObject prefab)
    {
        TerrainData terrainData = targetTerrain != null ? targetTerrain.terrainData : null;
        TreePrototype[] prototypes = terrainData != null ? terrainData.treePrototypes : null;
        prefab = null;
        if (prototypes == null || prototypeIndex < 0 || prototypeIndex >= prototypes.Length)
        {
            return false;
        }

        prefab = prototypes[prototypeIndex].prefab;
        return prefab != null && CountTreePrototypeOccurrences(prototypes, prefab) == 1;
    }

    private static int CountTreePrototypeOccurrences(
        IReadOnlyList<TreePrototype> prototypes,
        GameObject prefab)
    {
        if (prefab == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < prototypes.Count; i++)
        {
            if (prototypes[i].prefab == prefab)
            {
                count++;
            }
        }

        return count;
    }

    private void ApplyPresetPickerChange()
    {
        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        RebuildPresetSerializedObject();
        Repaint();
        SceneView.RepaintAll();
    }

    private void AddAllTreePrototypesFromTerrain()
    {
        TreePrototype[] prototypes = targetTerrain.terrainData.treePrototypes;
        if (prototypes == null || prototypes.Length == 0)
        {
            return;
        }

        presetSerializedObject.Update();
        entriesProperty.ClearArray();
        for (int i = 0; i < prototypes.Length; i++)
        {
            entriesProperty.InsertArrayElementAtIndex(i);
            SetEntryDefaults(entriesProperty.GetArrayElementAtIndex(i), prototypes[i].prefab);
        }

        presetSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(preset);
        RebuildPresetSerializedObject();
        SceneView.RepaintAll();
    }

    private static void SetEntryDefaults(SerializedProperty entry, GameObject prefab)
    {
        entry.FindPropertyRelative("enabled").boolValue = true;
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("weight").floatValue = 1f;
        entry.FindPropertyRelative("randomRotation").boolValue = true;
        entry.FindPropertyRelative("minHeightScale").floatValue = 0.85f;
        entry.FindPropertyRelative("maxHeightScale").floatValue = 1.15f;
        entry.FindPropertyRelative("lockWidthToHeight").boolValue = true;
        entry.FindPropertyRelative("minWidthScale").floatValue = 0.85f;
        entry.FindPropertyRelative("maxWidthScale").floatValue = 1.15f;
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
        float closestDistance = float.MaxValue;

        foreach (Terrain candidate in Terrain.activeTerrains)
        {
            TerrainCollider collider = candidate != null ? candidate.GetComponent<TerrainCollider>() : null;
            if (candidate == null || candidate.terrainData == null || collider == null)
            {
                continue;
            }

            if (!collider.Raycast(ray, out RaycastHit hit, float.MaxValue) || hit.distance >= closestDistance)
            {
                continue;
            }

            terrain = candidate;
            hitPoint = hit.point;
            closestDistance = hit.distance;
        }

        if (terrain == null)
        {
            return false;
        }

        Vector3 local = terrain.transform.InverseTransformPoint(hitPoint);
        Vector3 size = terrain.terrainData.size;
        float normalizedX = size.x > 0f ? Mathf.Clamp01(local.x / size.x) : 0f;
        float normalizedZ = size.z > 0f ? Mathf.Clamp01(local.z / size.z) : 0f;
        hitNormal = terrain.transform.TransformDirection(
            terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ)).normalized;
        return true;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        float x = first.x - second.x;
        float z = first.z - second.z;
        return Mathf.Sqrt(x * x + z * z);
    }
}
}
#endif
