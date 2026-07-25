#if UNITY_EDITOR
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal sealed class RelativeHeightBrushEvaluator
{
    private readonly RelativeHeightBrushSettings settings;

    public RelativeHeightBrushEvaluator(RelativeHeightBrushSettings settings)
    {
        this.settings = settings;
    }

    public bool TryGetSample(
        Vector3 brushCenter,
        Vector3 fieldPatternOrigin,
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
        targetOffset = settings.HeightOffset;
        opacity = 0f;
        useSampleHeightAsBase = false;
        Vector3 offset = sampleWorld - brushCenter;
        offset.y = 0f;

        switch (settings.Shape)
        {
            case HeightBrushShape.Circle:
                float distance = offset.magnitude;
                if (distance > settings.Radius)
                {
                    return false;
                }

                opacity = ComputeEdgeOpacity(settings.Radius > 0f ? distance / settings.Radius : 0f);
                return true;

            case HeightBrushShape.Square:
                float squareEdge = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z));
                if (squareEdge > settings.HalfSize)
                {
                    return false;
                }

                opacity = ComputeEdgeOpacity(settings.HalfSize > 0f ? squareEdge / settings.HalfSize : 0f);
                return true;

            case HeightBrushShape.Slope:
                float along = Vector3.Dot(offset, slopeDirection);
                float across = Vector3.Dot(offset, slopeRight);
                float slopeEdge = Mathf.Max(Mathf.Abs(along), Mathf.Abs(across));
                if (slopeEdge > settings.HalfSize)
                {
                    return false;
                }

                targetOffset = Mathf.Lerp(
                    settings.LowerSlopeOffset,
                    settings.HigherSlopeOffset,
                    Mathf.InverseLerp(-settings.HalfSize, settings.HalfSize, along));
                opacity = ComputeEdgeOpacity(settings.HalfSize > 0f ? slopeEdge / settings.HalfSize : 0f);
                return true;

            case HeightBrushShape.SlopeCurve:
                float curveAlong = Vector3.Dot(offset, slopeDirection);
                float curveAcross = Vector3.Dot(offset, slopeRight);
                float curveEdge = Mathf.Max(Mathf.Abs(curveAlong), Mathf.Abs(curveAcross));
                if (curveEdge > settings.HalfSize)
                {
                    return false;
                }

                float curveT = Mathf.InverseLerp(-settings.HalfSize, settings.HalfSize, curveAlong);
                targetOffset = Mathf.Lerp(
                    settings.LowerSlopeOffset,
                    settings.HigherSlopeOffset,
                    EvaluateSlopeCurve(curveT));
                opacity = ComputeEdgeOpacity(settings.HalfSize > 0f ? curveEdge / settings.HalfSize : 0f);
                return true;

            case HeightBrushShape.FieldFurrows:
                float rowAlong = Vector3.Dot(offset, fieldDirection);
                float inclusionAcross = Vector3.Dot(offset, fieldRight);
                float fieldHalfLength = Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, settings.FieldLength) * 0.5f;
                float fieldHalfWidth = settings.HalfSize;
                float fieldEdge = Mathf.Max(
                    fieldHalfLength > 0f ? Mathf.Abs(rowAlong) / fieldHalfLength : 0f,
                    fieldHalfWidth > 0f ? Mathf.Abs(inclusionAcross) / fieldHalfWidth : 0f);
                if (fieldEdge > 1f)
                {
                    return false;
                }

                Vector3 patternOffset = sampleWorld - fieldPatternOrigin;
                patternOffset.y = 0f;
                float rowAcrossFromFieldEdge = Vector3.Dot(patternOffset, fieldRight) + fieldHalfWidth;
                targetOffset = -settings.FurrowDepth *
                    ComputeFurrowTrough(rowAcrossFromFieldEdge, furrowSampleHalfSpanRight);
                opacity = ComputeEdgeOpacity(fieldEdge);
                useSampleHeightAsBase = true;
                return true;

            case HeightBrushShape.SingleFurrow:
                float furrowAlong = Vector3.Dot(offset, fieldDirection);
                float furrowAcross = Vector3.Dot(offset, fieldRight);
                float furrowHalfLength =
                    Mathf.Max(RelativeHeightBrushSettings.MinimumBrushSize, settings.SingleFurrowLength) * 0.5f;
                float furrowHalfWidth = Mathf.Max(0.05f, settings.SingleFurrowWidth) * 0.5f;
                if (Mathf.Abs(furrowAlong) > furrowHalfLength + furrowSampleHalfSpanAlong ||
                    Mathf.Abs(furrowAcross) > furrowHalfWidth + furrowSampleHalfSpanRight)
                {
                    return false;
                }

                float furrowEdge = Mathf.Max(
                    furrowHalfLength > 0f
                        ? Mathf.Abs(furrowAlong) / Mathf.Max(furrowHalfLength, furrowSampleHalfSpanAlong)
                        : 0f,
                    furrowHalfWidth > 0f
                        ? Mathf.Abs(furrowAcross) / Mathf.Max(furrowHalfWidth, furrowSampleHalfSpanRight)
                        : 0f);
                targetOffset = -settings.SingleFurrowDepth *
                    ComputeSingleFurrowTrough(furrowAcross, furrowSampleHalfSpanRight);
                opacity = ComputeEdgeOpacity(furrowEdge);
                useSampleHeightAsBase = true;
                return true;

            default:
                return false;
        }
    }

    public float ComputeFurrowTrough(float distanceFromFieldEdge, float sampleHalfSpan)
    {
        float flatLength = Mathf.Max(0.01f, settings.FurrowSpacing);
        float furrowLength = Mathf.Max(0.01f, settings.FurrowWidth);
        float period = flatLength + furrowLength;
        float phase = Mathf.Repeat(distanceFromFieldEdge, period);
        if (sampleHalfSpan > 0.001f)
        {
            float furrowCenter = flatLength + furrowLength * 0.5f;
            float distanceToCenter =
                Mathf.Abs(Mathf.Repeat(phase - furrowCenter + period * 0.5f, period) - period * 0.5f);
            float furrowHalfWidth = furrowLength * 0.5f;
            float maxRepresentableHalfWidth = Mathf.Max(
                furrowHalfWidth,
                Mathf.Min(period * 0.45f, furrowHalfWidth + flatLength * 0.5f));
            float paintedHalfWidth =
                Mathf.Min(Mathf.Max(furrowHalfWidth, sampleHalfSpan), maxRepresentableHalfWidth);
            return distanceToCenter <= paintedHalfWidth ? 1f : 0f;
        }

        if (phase < flatLength)
        {
            return 0f;
        }

        float furrowPhase = phase - flatLength;
        float feather = Mathf.Clamp(settings.FurrowEdgeFeather, 0f, furrowLength * 0.5f);
        if (feather <= 0.001f)
        {
            return 1f;
        }

        if (furrowPhase < feather)
        {
            return Mathf.SmoothStep(0f, 1f, furrowPhase / feather);
        }

        if (furrowPhase > furrowLength - feather)
        {
            return 1f - Mathf.SmoothStep(
                0f,
                1f,
                (furrowPhase - (furrowLength - feather)) / feather);
        }

        return 1f;
    }

    public float ComputeSingleFurrowTrough(float across, float sampleHalfSpan)
    {
        float halfWidth = Mathf.Max(0.05f, settings.SingleFurrowWidth) * 0.5f;
        float paintedHalfWidth = Mathf.Max(halfWidth, sampleHalfSpan);
        float distanceFromCenter = Mathf.Abs(across);
        if (distanceFromCenter > paintedHalfWidth)
        {
            return 0f;
        }

        float feather = Mathf.Clamp(settings.SingleFurrowEdgeFeather, 0f, halfWidth);
        if (feather <= 0.001f || sampleHalfSpan > halfWidth)
        {
            return 1f;
        }

        float featherStart = Mathf.Max(0f, halfWidth - feather);
        return distanceFromCenter <= featherStart
            ? 1f
            : 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(featherStart, halfWidth, distanceFromCenter));
    }

    public float EvaluateSlopeCurve(float t)
    {
        settings.EnsureSlopeCurve();
        return Mathf.Clamp01(settings.SlopeCurve.Evaluate(Mathf.Clamp01(t)));
    }

    public float ComputeEdgeOpacity(float normalizedEdgeDistance)
    {
        float strength = Mathf.Clamp01(settings.Strength);
        if (strength <= 0f)
        {
            return 0f;
        }

        float blend = Mathf.Clamp01(settings.EdgeBlend);
        if (blend <= 0.001f)
        {
            return strength;
        }

        float innerEdge = 1f - blend;
        float clampedDistance = Mathf.Clamp01(normalizedEdgeDistance);
        if (clampedDistance <= innerEdge)
        {
            return strength;
        }

        float edgeT = 1f - Mathf.InverseLerp(innerEdge, 1f, clampedDistance);
        return strength * Mathf.SmoothStep(0f, 1f, edgeT);
    }
}
}
#endif
