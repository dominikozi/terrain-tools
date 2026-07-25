#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
public sealed partial class RelativeTerrainHeightToolWindow
{
    private void DrawBrushPreview(Terrain hitTerrain, Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 normal = hitNormal.sqrMagnitude > 0f ? hitNormal.normalized : Vector3.up;
        Vector3 center = hitPoint + normal * BrushPreviewOffset;
        Color previous = Handles.color;

        if (settings.Shape == HeightBrushShape.Circle)
        {
            Handles.color = new Color(0.95f, 0.75f, 0.15f, 0.12f);
            Handles.DrawSolidDisc(center, Vector3.up, BrushRadius);
            Handles.color = new Color(1f, 0.78f, 0.15f, 0.9f);
            Handles.DrawWireDisc(center, Vector3.up, BrushRadius);
        }
        else
        {
            Vector3 direction = settings.Shape switch
            {
                HeightBrushShape.Slope => GetSlopeDirection(),
                HeightBrushShape.SlopeCurve => GetSlopeDirection(),
                HeightBrushShape.FieldFurrows => GetFieldDirection(),
                HeightBrushShape.SingleFurrow => GetFieldDirection(),
                _ => Vector3.forward
            };
            Vector3 right = new(direction.z, 0f, -direction.x);
            Vector3[] corners = BuildSquareCorners(center, direction, right);

            Handles.color = GetSquareFillColor();
            Handles.DrawAAConvexPolygon(corners);

            Handles.color = GetSquareOutlineColor();
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);

            if (IsSlopeBrush(settings.Shape))
            {
                DrawSlopeDirectionPreview(center, direction, right);
            }
            else if (settings.Shape == HeightBrushShape.FieldFurrows)
            {
                DrawFieldFurrowPreview(center, direction, right);
            }
            else if (settings.Shape == HeightBrushShape.SingleFurrow)
            {
                DrawSingleFurrowPreview(center, direction, right);
            }
        }

        DrawHeightPreview(hitTerrain, hitPoint);
        Handles.color = previous;
    }

    private void DrawHeightPreview(Terrain hitTerrain, Vector3 hitPoint)
    {
        if (!showHeightPreview || hitTerrain == null || hitTerrain.terrainData == null)
        {
            return;
        }

        Vector3 primaryDirection = GetPreviewPrimaryDirection();
        Vector3 right = new(primaryDirection.z, 0f, -primaryDirection.x);
        Vector3 slopeDirection = GetSlopeDirection();
        Vector3 slopeRight = new(slopeDirection.z, 0f, -slopeDirection.x);
        Vector3 fieldDirection = GetFieldDirection();
        Vector3 fieldRight = new(fieldDirection.z, 0f, -fieldDirection.x);
        float referenceWorldY = GetStrokeReferenceWorldY(hitTerrain, hitPoint, slopeDirection);
        float extent = GetBrushBoundsExtent();
        int resolution = Mathf.Clamp(heightPreviewResolution, MinPreviewResolution, MaxPreviewResolution);

        Handles.color = new Color(0.25f, 1f, 0.15f, 0.85f);
        DrawHeightPreviewLines(
            hitTerrain,
            hitPoint,
            primaryDirection,
            right,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            referenceWorldY,
            extent,
            resolution,
            true);

        Handles.color = new Color(0.2f, 0.95f, 0.1f, 0.55f);
        DrawHeightPreviewLines(
            hitTerrain,
            hitPoint,
            primaryDirection,
            right,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            referenceWorldY,
            extent,
            resolution,
            false);

        DrawHeightPreviewSides(
            hitTerrain,
            hitPoint,
            primaryDirection,
            right,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            referenceWorldY,
            resolution);
    }

    private void DrawHeightPreviewLines(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 primaryDirection,
        Vector3 right,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        float extent,
        int resolution,
        bool rows)
    {
        List<Vector3> segmentPoints = new(resolution + 1);

        for (int outer = 0; outer <= resolution; outer++)
        {
            float outerOffset = Mathf.Lerp(-extent, extent, outer / (float)resolution);
            segmentPoints.Clear();

            for (int inner = 0; inner <= resolution; inner++)
            {
                float innerOffset = Mathf.Lerp(-extent, extent, inner / (float)resolution);
                Vector3 samplePosition = rows
                    ? hitPoint + primaryDirection * innerOffset + right * outerOffset
                    : hitPoint + primaryDirection * outerOffset + right * innerOffset;

                if (!TryGetHeightPreviewPoint(
                    hitTerrain,
                    hitPoint,
                    samplePosition,
                    slopeDirection,
                    slopeRight,
                    fieldDirection,
                    fieldRight,
                    referenceWorldY,
                    out Vector3 previewPoint,
                    out _))
                {
                    FlushHeightPreviewSegment(segmentPoints);
                    continue;
                }

                segmentPoints.Add(previewPoint);
            }

            FlushHeightPreviewSegment(segmentPoints);
        }
    }

    private bool TryGetHeightPreviewPoint(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 samplePosition,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        out Vector3 previewPoint,
        out Vector3 terrainPoint)
    {
        previewPoint = default;
        terrainPoint = default;
        if (!TryGetBrushSample(
            hitPoint,
            samplePosition,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            0f,
            0f,
            out float targetOffset,
            out float opacity,
            out bool useSampleHeightAsBase))
        {
            return false;
        }

        if (!TrySamplePreviewTerrainWorldY(hitTerrain, samplePosition, out float currentWorldY))
        {
            return false;
        }

        float targetWorldY = (useSampleHeightAsBase ? currentWorldY : referenceWorldY) + targetOffset;
        terrainPoint = new Vector3(samplePosition.x, currentWorldY + BrushPreviewOffset, samplePosition.z);
        previewPoint = new Vector3(
            samplePosition.x,
            Mathf.Lerp(currentWorldY, targetWorldY, opacity) + BrushPreviewOffset,
            samplePosition.z);
        return true;
    }

    private void DrawHeightPreviewSides(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 primaryDirection,
        Vector3 right,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        int resolution)
    {
        if (settings.Shape == HeightBrushShape.Circle)
        {
            DrawCircularHeightPreviewSides(
                hitTerrain,
                hitPoint,
                slopeDirection,
                slopeRight,
                fieldDirection,
                fieldRight,
                referenceWorldY,
                resolution);
            return;
        }

        DrawSquareHeightPreviewSides(
            hitTerrain,
            hitPoint,
            primaryDirection,
            right,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            referenceWorldY,
            resolution);
    }

    private void DrawCircularHeightPreviewSides(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        int resolution)
    {
        int samples = Mathf.Max(16, resolution * 4);
        List<Vector3> topPoints = new(samples + 1);
        List<Vector3> bottomPoints = new(samples + 1);

        for (int i = 0; i <= samples; i++)
        {
            float angle = i / (float)samples * Mathf.PI * 2f;
            Vector3 samplePosition = hitPoint + new Vector3(Mathf.Cos(angle) * BrushRadius, 0f, Mathf.Sin(angle) * BrushRadius);
            AddHeightPreviewSideSample(
                hitTerrain,
                hitPoint,
                samplePosition,
                slopeDirection,
                slopeRight,
                fieldDirection,
                fieldRight,
                referenceWorldY,
                topPoints,
                bottomPoints,
                i % Mathf.Max(1, samples / 16) == 0);
        }

        DrawHeightPreviewSideOutlines(topPoints, bottomPoints);
    }

    private void DrawSquareHeightPreviewSides(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 primaryDirection,
        Vector3 right,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        int resolution)
    {
        float directionHalf = GetPreviewDirectionHalfSize();
        float rightHalf = GetPreviewRightHalfSize();
        Vector3[] corners =
        {
            hitPoint - primaryDirection * directionHalf - right * rightHalf,
            hitPoint - primaryDirection * directionHalf + right * rightHalf,
            hitPoint + primaryDirection * directionHalf + right * rightHalf,
            hitPoint + primaryDirection * directionHalf - right * rightHalf
        };

        for (int edge = 0; edge < corners.Length; edge++)
        {
            Vector3 start = corners[edge];
            Vector3 end = corners[(edge + 1) % corners.Length];
            List<Vector3> topPoints = new(resolution + 1);
            List<Vector3> bottomPoints = new(resolution + 1);

            for (int i = 0; i <= resolution; i++)
            {
                Vector3 samplePosition = Vector3.Lerp(start, end, i / (float)resolution);
                AddHeightPreviewSideSample(
                    hitTerrain,
                    hitPoint,
                    samplePosition,
                    slopeDirection,
                    slopeRight,
                    fieldDirection,
                    fieldRight,
                    referenceWorldY,
                    topPoints,
                    bottomPoints,
                    i == 0 || i == resolution || i % Mathf.Max(1, resolution / 8) == 0);
            }

            DrawHeightPreviewSideOutlines(topPoints, bottomPoints);
        }
    }

    private void AddHeightPreviewSideSample(
        Terrain hitTerrain,
        Vector3 hitPoint,
        Vector3 samplePosition,
        Vector3 slopeDirection,
        Vector3 slopeRight,
        Vector3 fieldDirection,
        Vector3 fieldRight,
        float referenceWorldY,
        List<Vector3> topPoints,
        List<Vector3> bottomPoints,
        bool drawVertical)
    {
        if (!TryGetHeightPreviewPoint(
            hitTerrain,
            hitPoint,
            samplePosition,
            slopeDirection,
            slopeRight,
            fieldDirection,
            fieldRight,
            referenceWorldY,
            out Vector3 previewPoint,
            out Vector3 terrainPoint))
        {
            DrawHeightPreviewSideOutlines(topPoints, bottomPoints);
            return;
        }

        topPoints.Add(previewPoint);
        bottomPoints.Add(terrainPoint);

        if (!drawVertical)
        {
            return;
        }

        Color previous = Handles.color;
        Handles.color = new Color(0.15f, 0.8f, 0.08f, 0.42f);
        Handles.DrawAAPolyLine(2f, terrainPoint, previewPoint);
        Handles.color = previous;
    }

    private void DrawHeightPreviewSideOutlines(List<Vector3> topPoints, List<Vector3> bottomPoints)
    {
        if (topPoints.Count >= 2)
        {
            Color previous = Handles.color;
            Handles.color = new Color(0.15f, 0.95f, 0.08f, 0.72f);
            Handles.DrawAAPolyLine(2.5f, topPoints.ToArray());
            Handles.color = new Color(0.08f, 0.65f, 0.04f, 0.5f);
            Handles.DrawAAPolyLine(2f, bottomPoints.ToArray());
            Handles.color = previous;
        }

        topPoints.Clear();
        bottomPoints.Clear();
    }

    private void FlushHeightPreviewSegment(List<Vector3> segmentPoints)
    {
        if (segmentPoints.Count < 2)
        {
            segmentPoints.Clear();
            return;
        }

        Handles.DrawAAPolyLine(2f, segmentPoints.ToArray());
        segmentPoints.Clear();
    }

    private bool TrySamplePreviewTerrainWorldY(Terrain hitTerrain, Vector3 worldPosition, out float worldY)
    {
        if (paintAcrossActiveTerrains && TrySampleActiveTerrainWorldY(worldPosition, out worldY))
        {
            return true;
        }

        return TrySampleTerrainWorldY(hitTerrain, worldPosition, out worldY);
    }

    private Vector3 GetPreviewPrimaryDirection()
    {
        return settings.Shape switch
        {
            HeightBrushShape.Slope => GetSlopeDirection(),
            HeightBrushShape.SlopeCurve => GetSlopeDirection(),
            HeightBrushShape.FieldFurrows => GetFieldDirection(),
            HeightBrushShape.SingleFurrow => GetFieldDirection(),
            _ => Vector3.forward
        };
    }

    private Color GetSquareFillColor()
    {
        return settings.Shape switch
        {
            HeightBrushShape.Slope => new Color(0.35f, 0.75f, 1f, 0.14f),
            HeightBrushShape.SlopeCurve => new Color(0.45f, 0.45f, 1f, 0.14f),
            HeightBrushShape.FieldFurrows => new Color(0.45f, 0.75f, 0.22f, 0.14f),
            HeightBrushShape.SingleFurrow => new Color(0.65f, 0.45f, 0.2f, 0.14f),
            _ => new Color(0.95f, 0.75f, 0.15f, 0.12f)
        };
    }

    private Color GetSquareOutlineColor()
    {
        return settings.Shape switch
        {
            HeightBrushShape.Slope => new Color(0.35f, 0.8f, 1f, 0.95f),
            HeightBrushShape.SlopeCurve => new Color(0.55f, 0.55f, 1f, 0.95f),
            HeightBrushShape.FieldFurrows => new Color(0.5f, 0.8f, 0.25f, 0.95f),
            HeightBrushShape.SingleFurrow => new Color(0.9f, 0.55f, 0.18f, 0.95f),
            _ => new Color(1f, 0.78f, 0.15f, 0.9f)
        };
    }

    private void DrawSlopeDirectionPreview(Vector3 center, Vector3 direction, Vector3 right)
    {
        float half = BrushHalfSize;
        Vector3 lowerPoint = center - direction * half;
        Vector3 higherPoint = center + direction * half;

        Handles.color = new Color(0.1f, 0.35f, 1f, 0.95f);
        Handles.DrawLine(lowerPoint, higherPoint);
        Handles.SphereHandleCap(0, lowerPoint, Quaternion.identity, Mathf.Max(0.15f, settings.Size * 0.035f), EventType.Repaint);

        Handles.color = new Color(1f, 0.25f, 0.1f, 0.95f);
        Handles.ConeHandleCap(0, higherPoint, Quaternion.LookRotation(direction, Vector3.up), Mathf.Max(0.25f, settings.Size * 0.06f), EventType.Repaint);

        if (settings.Shape == HeightBrushShape.SlopeCurve)
        {
            DrawSlopeCurvePreview(center, direction, right);
        }
    }

    private void DrawSlopeCurvePreview(Vector3 center, Vector3 direction, Vector3 right)
    {
        const int SegmentCount = 24;
        float half = BrushHalfSize;
        float sideOffset = half * 0.35f;
        float heightScale = Mathf.Max(0.5f, Mathf.Min(settings.Size * 0.15f, Mathf.Abs(settings.HigherSlopeOffset - settings.LowerSlopeOffset) * 0.15f));
        Vector3[] points = new Vector3[SegmentCount + 1];

        for (int i = 0; i <= SegmentCount; i++)
        {
            float t = i / (float)SegmentCount;
            float along = Mathf.Lerp(-half, half, t);
            float curve = EvaluateSlopeCurve(t);
            points[i] = center + direction * along + right * sideOffset + Vector3.up * curve * heightScale;
        }

        Handles.color = new Color(0.9f, 0.95f, 1f, 0.95f);
        Handles.DrawAAPolyLine(2.5f, points);
    }

    private void DrawFieldFurrowPreview(Vector3 center, Vector3 rowDirection, Vector3 rowRight)
    {
        float halfLength = GetFieldHalfLength();
        float halfWidth = GetFieldHalfWidth();
        float flatLength = Mathf.Max(0.01f, settings.FurrowSpacing);
        float furrowLength = Mathf.Max(0.01f, settings.FurrowWidth);
        float period = flatLength + furrowLength;
        Vector3 patternOffset = center - GetFieldPatternOrigin(center);
        patternOffset.y = 0f;
        float centerAcrossPattern = Vector3.Dot(patternOffset, rowRight);
        int minCycle = Mathf.FloorToInt((centerAcrossPattern - halfWidth) / period) - 1;
        int maxCycle = Mathf.CeilToInt((centerAcrossPattern + halfWidth) / period) + 1;

        Handles.color = new Color(0.2f, 0.14f, 0.05f, 0.85f);
        for (int cycle = minCycle; cycle <= maxCycle; cycle++)
        {
            float furrowStart = cycle * period + flatLength - centerAcrossPattern;
            float furrowEnd = furrowStart + furrowLength;
            float clippedStart = Mathf.Max(furrowStart, -halfWidth);
            float clippedEnd = Mathf.Min(furrowEnd, halfWidth);
            if (clippedEnd <= clippedStart)
            {
                continue;
            }

            Vector3 startCenter = center + rowRight * clippedStart;
            Vector3 endCenter = center + rowRight * clippedEnd;
            Handles.DrawLine(startCenter - rowDirection * halfLength, startCenter + rowDirection * halfLength);
            Handles.DrawLine(endCenter - rowDirection * halfLength, endCenter + rowDirection * halfLength);
        }
    }

    private void DrawSingleFurrowPreview(Vector3 center, Vector3 direction, Vector3 right)
    {
        float halfLength = GetSingleFurrowHalfLength();
        float halfWidth = GetSingleFurrowHalfWidth();
        Vector3 leftCenter = center - right * halfWidth;
        Vector3 rightCenter = center + right * halfWidth;

        Handles.color = new Color(0.18f, 0.09f, 0.02f, 0.85f);
        Handles.DrawLine(leftCenter - direction * halfLength, leftCenter + direction * halfLength);
        Handles.DrawLine(rightCenter - direction * halfLength, rightCenter + direction * halfLength);
        Handles.DrawLine(center - direction * halfLength, center + direction * halfLength);
    }

    private Vector3[] BuildSquareCorners(Vector3 center, Vector3 direction, Vector3 right)
    {
        float directionHalf = GetPreviewDirectionHalfSize();
        float rightHalf = GetPreviewRightHalfSize();
        return new[]
        {
            center - direction * directionHalf - right * rightHalf,
            center - direction * directionHalf + right * rightHalf,
            center + direction * directionHalf + right * rightHalf,
            center + direction * directionHalf - right * rightHalf
        };
    }

}
}
#endif
