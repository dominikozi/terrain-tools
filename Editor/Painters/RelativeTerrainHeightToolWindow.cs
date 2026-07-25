#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

public sealed partial class RelativeTerrainHeightToolWindow : EditorWindow
{
    private const string MenuPath = "Tools/Terrain Tools/Painters/Relative Height Brush";
    private const string UndoName = "Relative Terrain Height Brush";
    private const float MaxBrushSize = 512f;
    private const float BrushPreviewOffset = 0.04f;
    private const int MinPreviewResolution = 4;
    private const int MaxPreviewResolution = 48;

    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private bool paintingEnabled = true;
    [SerializeField] private bool paintAcrossActiveTerrains = true;
    [SerializeField] private bool lockReferenceHeightDuringStroke = true;
    [SerializeField] private bool showHeightPreview = true;
    [SerializeField] private int heightPreviewResolution = 20;
    [SerializeField] private RelativeHeightBrushSettings settings = new();

    private bool isPainting;
    private bool hasStrokeReferenceWorldY;
    private float strokeReferenceWorldY;
    private bool hasStrokeBrushOrigin;
    private Vector3 strokeBrushOrigin;
    private readonly TerrainPaintUndoTransaction undoTransaction = new();
    private readonly HashSet<TerrainData> delayedHeightmapTerrainData = new();
    private RelativeHeightBrushEvaluator brushEvaluator;

    private float BrushRadius => settings.Radius;
    private float BrushHalfSize => settings.HalfSize;

    [MenuItem(MenuPath, priority = 2412)]
    private static void Open()
    {
        RelativeTerrainHeightToolWindow window = GetWindow<RelativeTerrainHeightToolWindow>("Relative Height Brush");
        window.Show();
    }

    private void OnEnable()
    {
        settings ??= new RelativeHeightBrushSettings();
        brushEvaluator = new RelativeHeightBrushEvaluator(settings);
        SceneView.duringSceneGui += DuringSceneGui;
        TryUseSelectedTerrain();
        EnsureSlopeCurve();
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

        paintAcrossActiveTerrains = EditorGUILayout.Toggle("Paint Across Active Terrains", paintAcrossActiveTerrains);
        lockReferenceHeightDuringStroke = EditorGUILayout.Toggle("Lock Reference Height During Stroke", lockReferenceHeightDuringStroke);
        showHeightPreview = EditorGUILayout.Toggle("Show Live Height Preview", showHeightPreview);

        using (new EditorGUI.DisabledScope(!showHeightPreview))
        {
            heightPreviewResolution = EditorGUILayout.IntSlider(
                "Preview Resolution",
                heightPreviewResolution,
                MinPreviewResolution,
                MaxPreviewResolution);
        }

        EditorGUILayout.Space(8f);
        settings.Shape = (HeightBrushShape)EditorGUILayout.EnumPopup("Brush", settings.Shape);
        if (settings.Shape != HeightBrushShape.SingleFurrow)
        {
            settings.Size = EditorGUILayout.Slider(
                settings.Shape == HeightBrushShape.FieldFurrows ? "Field Width" : "Brush Size",
                settings.Size,
                RelativeHeightBrushSettings.MinimumBrushSize,
                MaxBrushSize);
        }

        settings.Strength = EditorGUILayout.Slider("Apply Strength", settings.Strength, 0f, 1f);
        settings.EdgeBlend = EditorGUILayout.Slider("Edge Blend", settings.EdgeBlend, 0f, 1f);

        if (IsSlopeBrush(settings.Shape))
        {
            settings.LowerSlopeOffset = EditorGUILayout.FloatField("Lower Slope Offset", settings.LowerSlopeOffset);
            settings.HigherSlopeOffset = EditorGUILayout.FloatField("Higher Slope Offset", settings.HigherSlopeOffset);
            settings.SlopeRotationDegrees = EditorGUILayout.Slider("Slope Rotation", NormalizeDegrees(settings.SlopeRotationDegrees), 0f, 360f);
            settings.SlopeUseLowerEdgeHeightReference = EditorGUILayout.Toggle("Use Lower Edge Height Reference", settings.SlopeUseLowerEdgeHeightReference);
            settings.SlopeRotationDegrees = DrawRotationButtons(settings.SlopeRotationDegrees);

            if (settings.Shape == HeightBrushShape.SlopeCurve)
            {
                EnsureSlopeCurve();
                settings.SlopeCurve = EditorGUILayout.CurveField("Slope Curve", settings.SlopeCurve, Color.cyan, new Rect(0f, 0f, 1f, 1f));
                if (GUILayout.Button("Reset Slope Curve"))
                {
                    settings.ResetSlopeCurve();
                }
            }
        }
        else if (settings.Shape == HeightBrushShape.FieldFurrows)
        {
            settings.FieldLength = Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, EditorGUILayout.FloatField("Field Length", settings.FieldLength));
            settings.FurrowDepth = Mathf.Max(0f, EditorGUILayout.FloatField("Furrow Depth", settings.FurrowDepth));
            settings.FurrowSpacing = Mathf.Max(0.01f, EditorGUILayout.FloatField("Flat Length Between Furrows", settings.FurrowSpacing));
            settings.FurrowWidth = Mathf.Clamp(
                EditorGUILayout.FloatField("Furrow Length", settings.FurrowWidth),
                0.05f,
                MaxBrushSize);
            settings.FurrowEdgeFeather = Mathf.Clamp(
                EditorGUILayout.FloatField("Furrow Edge Feather", settings.FurrowEdgeFeather),
                0f,
                Mathf.Max(0f, settings.FurrowWidth * 0.5f));
            settings.FieldRotationDegrees = EditorGUILayout.Slider("Field Rotation", NormalizeDegrees(settings.FieldRotationDegrees), 0f, 360f);
            settings.FieldRotationDegrees = DrawRotationButtons(settings.FieldRotationDegrees);
        }
        else if (settings.Shape == HeightBrushShape.SingleFurrow)
        {
            settings.SingleFurrowLength = Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, EditorGUILayout.FloatField("Furrow Length", settings.SingleFurrowLength));
            settings.SingleFurrowWidth = Mathf.Clamp(
                EditorGUILayout.FloatField("Furrow Width", settings.SingleFurrowWidth),
                0.05f,
                MaxBrushSize);
            settings.SingleFurrowDepth = Mathf.Max(0f, EditorGUILayout.FloatField("Furrow Depth", settings.SingleFurrowDepth));
            settings.SingleFurrowEdgeFeather = Mathf.Clamp(
                EditorGUILayout.FloatField("Furrow Edge Feather", settings.SingleFurrowEdgeFeather),
                0f,
                Mathf.Max(0f, settings.SingleFurrowWidth * 0.5f));
            settings.SingleFurrowRotationDegrees = EditorGUILayout.Slider("Furrow Rotation", NormalizeDegrees(settings.SingleFurrowRotationDegrees), 0f, 360f);
            settings.SingleFurrowRotationDegrees = DrawRotationButtons(settings.SingleFurrowRotationDegrees);
        }
        else
        {
            settings.HeightOffset = EditorGUILayout.FloatField("Height Offset", settings.HeightOffset);
        }

        EditorGUILayout.Space(8f);
        DrawValidationMessages();
    }

    private void DrawValidationMessages()
    {
        Terrain[] activeTerrains = Terrain.activeTerrains;
        if ((activeTerrains == null || activeTerrains.Length == 0) && targetTerrain == null)
        {
            EditorGUILayout.HelpBox("No active Terrain found in the scene.", MessageType.Info);
            return;
        }

        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Select or hover a Terrain in the Scene view before painting.", MessageType.Info);
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData == null)
        {
            EditorGUILayout.HelpBox("The selected Terrain has no TerrainData.", MessageType.Warning);
            return;
        }

        if (terrainData.heightmapResolution <= 0 || terrainData.size.y <= 0f)
        {
            EditorGUILayout.HelpBox("The selected TerrainData has no usable heightmap.", MessageType.Warning);
            return;
        }

        if (settings.Shape == HeightBrushShape.FieldFurrows || settings.Shape == HeightBrushShape.SingleFurrow)
        {
            Vector3 fieldDirection = GetFieldDirection();
            float sampleSpacing = ComputeHeightmapSampleSpacingAlongDirection(
                targetTerrain,
                new Vector3(fieldDirection.z, 0f, -fieldDirection.x));
            float targetWidth = settings.Shape == HeightBrushShape.FieldFurrows ? settings.FurrowWidth : settings.SingleFurrowWidth;
            string targetWidthLabel = settings.Shape == HeightBrushShape.FieldFurrows ? "Furrow Length" : "Furrow Width";
            if (sampleSpacing > targetWidth)
            {
                EditorGUILayout.HelpBox(
                    $"{targetWidthLabel} ({targetWidth:0.##}m) is smaller than this Terrain heightmap spacing ({sampleSpacing:0.##}m), so painted furrows are snapped to the nearest height samples.",
                    MessageType.Info);
            }
        }

        EditorGUILayout.HelpBox(
            "Left mouse paints. The live height preview is a Scene view overlay only; TerrainData changes only while painting.",
            MessageType.None);
    }

    private static float DrawRotationButtons(float degrees)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("0"))
            {
                degrees = 0f;
            }

            if (GUILayout.Button("45"))
            {
                degrees = 45f;
            }

            if (GUILayout.Button("90"))
            {
                degrees = 90f;
            }

            if (GUILayout.Button("135"))
            {
                degrees = 135f;
            }

            if (GUILayout.Button("180"))
            {
                degrees = 180f;
            }

            if (GUILayout.Button("270"))
            {
                degrees = 270f;
            }
        }

        return degrees;
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

        DrawBrushPreview(hitTerrain, hitPoint, hitNormal);
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
            PaintAt(hitTerrain, hitPoint);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && isPainting)
        {
            PaintAt(hitTerrain, hitPoint);
            current.Use();
        }
        else if (current.rawType == EventType.MouseUp)
        {
            EndPaintStroke();
        }
    }

    private void BeginPaintStroke()
    {
        if (isPainting)
        {
            return;
        }

        undoTransaction.Begin(UndoName);
        delayedHeightmapTerrainData.Clear();
        hasStrokeReferenceWorldY = false;
        hasStrokeBrushOrigin = false;
        isPainting = true;
    }

    private void EndPaintStroke()
    {
        if (!isPainting)
        {
            return;
        }

        try
        {
            foreach (TerrainData terrainData in delayedHeightmapTerrainData)
            {
                if (terrainData == null)
                {
                    continue;
                }

                terrainData.SyncHeightmap();
            }

            undoTransaction.Complete();
        }
        catch (Exception exception)
        {
            undoTransaction.Revert();
            Debug.LogException(exception);
        }
        finally
        {
            isPainting = false;
            hasStrokeReferenceWorldY = false;
            hasStrokeBrushOrigin = false;
            delayedHeightmapTerrainData.Clear();
        }
    }

    private void RegisterTerrainUndo(Terrain terrain)
    {
        TerrainData terrainData = terrain != null ? terrain.terrainData : null;
        undoTransaction.Register(terrainData);
    }

    private void PaintAt(Terrain hitTerrain, Vector3 hitPoint)
    {
        try
        {
            Vector3 slopeDirection = GetSlopeDirection();
            float referenceWorldY = GetStrokeReferenceWorldY(hitTerrain, hitPoint, slopeDirection);
            Terrain[] terrains = paintAcrossActiveTerrains ? Terrain.activeTerrains : null;
            if (terrains == null || terrains.Length == 0)
            {
                TryPaintTerrain(hitTerrain, hitPoint, referenceWorldY);
                return;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                TryPaintTerrain(terrains[i], hitPoint, referenceWorldY);
            }
        }
        catch (Exception exception)
        {
            AbortPaintStroke(exception);
        }
    }

    private void AbortPaintStroke(Exception exception)
    {
        undoTransaction.Revert();
        isPainting = false;
        hasStrokeReferenceWorldY = false;
        hasStrokeBrushOrigin = false;
        delayedHeightmapTerrainData.Clear();
        Debug.LogException(exception);
    }

    private float GetStrokeReferenceWorldY(Terrain hitTerrain, Vector3 hitPoint, Vector3 slopeDirection)
    {
        float currentReferenceWorldY = GetReferenceWorldY(hitTerrain, hitPoint, slopeDirection);
        if (!lockReferenceHeightDuringStroke || !isPainting)
        {
            return currentReferenceWorldY;
        }

        if (!hasStrokeReferenceWorldY)
        {
            strokeReferenceWorldY = currentReferenceWorldY;
            hasStrokeReferenceWorldY = true;
        }

        return strokeReferenceWorldY;
    }

    private void TryPaintTerrain(Terrain terrain, Vector3 hitPoint, float referenceWorldY)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        Vector3 slopeDirection = GetSlopeDirection();
        Vector3 fieldDirection = GetFieldDirection();
        if (RelativeHeightPaintService.PaintTerrain(
                terrain,
                hitPoint,
                GetFieldPatternOrigin(hitPoint),
                referenceWorldY,
                GetBrushBoundsExtent(),
                slopeDirection,
                fieldDirection,
                settings,
                brushEvaluator,
                RegisterTerrainUndo))
        {
            delayedHeightmapTerrainData.Add(terrain.terrainData);
        }
    }

    private bool TryGetBrushSample(
        Vector3 brushCenter,
        Vector3 sampleWorld,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float furrowSampleHalfSpanAlong,
        float furrowSampleHalfSpanRight,
        out float targetOffset,
        out float opacity,
        out bool useSampleHeightAsBase)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        return brushEvaluator.TryGetSample(
            brushCenter,
            GetFieldPatternOrigin(brushCenter),
            sampleWorld,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            furrowSampleHalfSpanAlong,
            furrowSampleHalfSpanRight,
            out targetOffset,
            out opacity,
            out useSampleHeightAsBase);
    }

    private float ComputeFurrowTrough(float distanceFromFieldEdge, float sampleHalfSpan)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        return brushEvaluator.ComputeFurrowTrough(distanceFromFieldEdge, sampleHalfSpan);
    }

    private float ComputeSingleFurrowTrough(float across, float sampleHalfSpan)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        return brushEvaluator.ComputeSingleFurrowTrough(across, sampleHalfSpan);
    }

    private float EvaluateSlopeCurve(float t)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        return brushEvaluator.EvaluateSlopeCurve(t);
    }

    private float ComputeEdgeOpacity(float normalizedEdgeDistance)
    {
        brushEvaluator ??= new RelativeHeightBrushEvaluator(settings);
        return brushEvaluator.ComputeEdgeOpacity(normalizedEdgeDistance);
    }

    private Vector3 GetSlopeDirection()
    {
        Vector3 direction = Quaternion.Euler(0f, NormalizeDegrees(settings.SlopeRotationDegrees), 0f) * Vector3.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
    }

    private float GetReferenceWorldY(Terrain terrain, Vector3 hitPoint, Vector3 slopeDirection)
    {
        if (!IsSlopeBrush(settings.Shape) || !settings.SlopeUseLowerEdgeHeightReference)
        {
            return hitPoint.y;
        }

        Vector3 lowerReferencePoint = hitPoint - slopeDirection * BrushHalfSize;
        if (TrySampleActiveTerrainWorldY(lowerReferencePoint, out float sampledWorldY))
        {
            return sampledWorldY;
        }

        return TrySampleTerrainWorldY(terrain, lowerReferencePoint, out sampledWorldY)
            ? sampledWorldY
            : hitPoint.y;
    }

    private Vector3 GetFieldDirection()
    {
        float rotation = settings.Shape == HeightBrushShape.SingleFurrow
            ? settings.SingleFurrowRotationDegrees
            : settings.FieldRotationDegrees;
        Vector3 direction = Quaternion.Euler(0f, NormalizeDegrees(rotation), 0f) * Vector3.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
    }

    private Vector3 GetFieldPatternOrigin(Vector3 brushCenter)
    {
        if (settings.Shape != HeightBrushShape.FieldFurrows || !lockReferenceHeightDuringStroke || !isPainting)
        {
            return brushCenter;
        }

        if (!hasStrokeBrushOrigin)
        {
            strokeBrushOrigin = brushCenter;
            hasStrokeBrushOrigin = true;
        }

        return strokeBrushOrigin;
    }

    private float GetBrushBoundsExtent()
    {
        if (settings.Shape == HeightBrushShape.FieldFurrows)
        {
            float halfLength = GetFieldHalfLength();
            float halfWidth = GetFieldHalfWidth();
            return Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth);
        }

        if (settings.Shape == HeightBrushShape.SingleFurrow)
        {
            float halfLength = GetSingleFurrowHalfLength();
            float halfWidth = GetSingleFurrowHalfWidth();
            return Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth);
        }

        if (IsSlopeBrush(settings.Shape))
        {
            return BrushHalfSize * 1.415f;
        }

        return settings.Shape == HeightBrushShape.Circle ? BrushRadius : BrushHalfSize;
    }

    private float GetPreviewDirectionHalfSize()
    {
        return settings.Shape switch
        {
            HeightBrushShape.FieldFurrows => GetFieldHalfLength(),
            HeightBrushShape.SingleFurrow => GetSingleFurrowHalfLength(),
            _ => BrushHalfSize
        };
    }

    private float GetPreviewRightHalfSize()
    {
        return settings.Shape switch
        {
            HeightBrushShape.FieldFurrows => GetFieldHalfWidth(),
            HeightBrushShape.SingleFurrow => GetSingleFurrowHalfWidth(),
            _ => BrushHalfSize
        };
    }

    private float GetFieldHalfLength()
    {
        return Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, settings.FieldLength) * 0.5f;
    }

    private float GetFieldHalfWidth()
    {
        return BrushHalfSize;
    }

    private float GetSingleFurrowHalfLength()
    {
        return Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, settings.SingleFurrowLength) * 0.5f;
    }

    private float GetSingleFurrowHalfWidth()
    {
        return Mathf.Max(0.05f, settings.SingleFurrowWidth) * 0.5f;
    }

    private void EnsureSlopeCurve()
    {
        settings.EnsureSlopeCurve();
    }

    private static bool IsSlopeBrush(HeightBrushShape shape)
    {
        return shape == HeightBrushShape.Slope || shape == HeightBrushShape.SlopeCurve;
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
        hitPoint = closestHit.point;
        if (WorldToTerrainNormalized(terrain, hitPoint, out Vector2 normalized))
        {
            hitNormal = terrain.transform.TransformDirection(
                terrain.terrainData.GetInterpolatedNormal(normalized.x, normalized.y)).normalized;
        }
        else
        {
            hitNormal = closestHit.normal;
        }

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

    private static bool TrySampleActiveTerrainWorldY(Vector3 worldPosition, out float worldY)
    {
        worldY = default;
        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (TrySampleTerrainWorldY(terrains[i], worldPosition, out worldY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySampleTerrainWorldY(Terrain terrain, Vector3 worldPosition, out float worldY)
    {
        worldY = default;
        if (!WorldToTerrainNormalized(terrain, worldPosition, out Vector2 normalized))
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 localPosition = new(
            normalized.x * terrainData.size.x,
            terrainData.GetInterpolatedHeight(normalized.x, normalized.y),
            normalized.y * terrainData.size.z);
        worldY = terrain.transform.TransformPoint(localPosition).y;
        return true;
    }

    private static float ComputeHeightmapSampleSpacingAlongDirection(Terrain terrain, Vector3 direction)
    {
        return RelativeHeightPaintService.ComputeHeightmapSampleSpacingAlongDirection(
            terrain,
            direction);
    }

    private static float NormalizeDegrees(float degrees)
    {
        float normalized = degrees % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }

}
}
#endif
