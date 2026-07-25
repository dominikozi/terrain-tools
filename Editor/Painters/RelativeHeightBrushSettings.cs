#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal enum HeightBrushShape
{
    Circle,
    Square,
    Slope,
    SlopeCurve,
    FieldFurrows,
    SingleFurrow
}

[Serializable]
internal sealed class RelativeHeightBrushSettings
{
    internal const float MinimumBrushSize = 0.25f;

    [SerializeField] private HeightBrushShape shape = HeightBrushShape.Circle;
    [SerializeField] private float size = 8f;
    [SerializeField] private float strength = 1f;
    [SerializeField] private float edgeBlend;
    [SerializeField] private float heightOffset = 1f;
    [SerializeField] private float lowerSlopeOffset;
    [SerializeField] private float higherSlopeOffset = 20f;
    [SerializeField] private float slopeRotationDegrees;
    [SerializeField] private bool slopeUseLowerEdgeHeightReference;
    [SerializeField] private AnimationCurve slopeCurve = CreateDefaultSlopeCurve();
    [SerializeField] private float furrowDepth = 0.35f;
    [SerializeField] private float furrowSpacing = 2f;
    [SerializeField] private float furrowWidth = 0.5f;
    [SerializeField] private float furrowEdgeFeather;
    [SerializeField] private float fieldLength = 32f;
    [SerializeField] private float fieldRotationDegrees;
    [SerializeField] private float singleFurrowLength = 8f;
    [SerializeField] private float singleFurrowWidth = 1f;
    [SerializeField] private float singleFurrowDepth = 0.35f;
    [SerializeField] private float singleFurrowEdgeFeather = 0.1f;
    [SerializeField] private float singleFurrowRotationDegrees;

    internal HeightBrushShape Shape { get => shape; set => shape = value; }
    internal float Size { get => size; set => size = value; }
    internal float Strength { get => strength; set => strength = value; }
    internal float EdgeBlend { get => edgeBlend; set => edgeBlend = value; }
    internal float HeightOffset { get => heightOffset; set => heightOffset = value; }
    internal float LowerSlopeOffset { get => lowerSlopeOffset; set => lowerSlopeOffset = value; }
    internal float HigherSlopeOffset { get => higherSlopeOffset; set => higherSlopeOffset = value; }
    internal float SlopeRotationDegrees { get => slopeRotationDegrees; set => slopeRotationDegrees = value; }
    internal bool SlopeUseLowerEdgeHeightReference
    {
        get => slopeUseLowerEdgeHeightReference;
        set => slopeUseLowerEdgeHeightReference = value;
    }
    internal AnimationCurve SlopeCurve { get => slopeCurve; set => slopeCurve = value; }
    internal float FurrowDepth { get => furrowDepth; set => furrowDepth = value; }
    internal float FurrowSpacing { get => furrowSpacing; set => furrowSpacing = value; }
    internal float FurrowWidth { get => furrowWidth; set => furrowWidth = value; }
    internal float FurrowEdgeFeather { get => furrowEdgeFeather; set => furrowEdgeFeather = value; }
    internal float FieldLength { get => fieldLength; set => fieldLength = value; }
    internal float FieldRotationDegrees { get => fieldRotationDegrees; set => fieldRotationDegrees = value; }
    internal float SingleFurrowLength { get => singleFurrowLength; set => singleFurrowLength = value; }
    internal float SingleFurrowWidth { get => singleFurrowWidth; set => singleFurrowWidth = value; }
    internal float SingleFurrowDepth { get => singleFurrowDepth; set => singleFurrowDepth = value; }
    internal float SingleFurrowEdgeFeather { get => singleFurrowEdgeFeather; set => singleFurrowEdgeFeather = value; }
    internal float SingleFurrowRotationDegrees
    {
        get => singleFurrowRotationDegrees;
        set => singleFurrowRotationDegrees = value;
    }

    internal float Radius => Mathf.Max(MinimumBrushSize, size) * 0.5f;
    internal float HalfSize => Mathf.Max(MinimumBrushSize, size) * 0.5f;

    internal void EnsureSlopeCurve()
    {
        if (slopeCurve == null || slopeCurve.length == 0)
        {
            slopeCurve = CreateDefaultSlopeCurve();
        }
    }

    internal void ResetSlopeCurve()
    {
        slopeCurve = CreateDefaultSlopeCurve();
    }

    private static AnimationCurve CreateDefaultSlopeCurve()
    {
        AnimationCurve curve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.22f, 0.06f),
            new Keyframe(0.58f, 0.55f),
            new Keyframe(0.84f, 0.9f),
            new Keyframe(1f, 1f));

        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0.35f);
        }

        return curve;
    }
}
}
#endif
