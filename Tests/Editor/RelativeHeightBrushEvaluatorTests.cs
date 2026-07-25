using Dominikozi.TerrainTools.Editor.Painters;
using NUnit.Framework;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
internal sealed class RelativeHeightBrushEvaluatorTests
{
    [TestCase(HeightBrushShape.Circle)]
    [TestCase(HeightBrushShape.Square)]
    [TestCase(HeightBrushShape.Slope)]
    [TestCase(HeightBrushShape.SlopeCurve)]
    [TestCase(HeightBrushShape.FieldFurrows)]
    [TestCase(HeightBrushShape.SingleFurrow)]
    public void EveryShape_AcceptsItsCenterSample(HeightBrushShape shape)
    {
        RelativeHeightBrushSettings settings = CreateSettings(shape);
        RelativeHeightBrushEvaluator evaluator = new RelativeHeightBrushEvaluator(settings);

        bool inside = evaluator.TryGetSample(
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.forward,
            Vector3.right,
            Vector3.forward,
            Vector3.right,
            0.05f,
            0.05f,
            out _,
            out float opacity,
            out _);

        Assert.That(inside, Is.True);
        Assert.That(opacity, Is.GreaterThan(0f));
    }

    [Test]
    public void Circle_EdgeBlendFadesTheBoundary()
    {
        RelativeHeightBrushSettings settings = CreateSettings(HeightBrushShape.Circle);
        settings.EdgeBlend = 0.5f;
        RelativeHeightBrushEvaluator evaluator = new RelativeHeightBrushEvaluator(settings);

        evaluator.TryGetSample(
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.forward,
            Vector3.right,
            Vector3.forward,
            Vector3.right,
            0f,
            0f,
            out _,
            out float centerOpacity,
            out _);
        evaluator.TryGetSample(
            Vector3.zero,
            Vector3.zero,
            Vector3.right * (settings.Radius * 0.95f),
            Vector3.forward,
            Vector3.right,
            Vector3.forward,
            Vector3.right,
            0f,
            0f,
            out _,
            out float edgeOpacity,
            out _);

        Assert.That(centerOpacity, Is.EqualTo(1f).Within(0.00001f));
        Assert.That(edgeOpacity, Is.LessThan(centerOpacity));
    }

    [Test]
    public void SlopeCurve_UsesConfiguredOffsets()
    {
        RelativeHeightBrushSettings settings = CreateSettings(HeightBrushShape.SlopeCurve);
        settings.LowerSlopeOffset = -2f;
        settings.HigherSlopeOffset = 6f;
        RelativeHeightBrushEvaluator evaluator = new RelativeHeightBrushEvaluator(settings);

        evaluator.TryGetSample(
            Vector3.zero,
            Vector3.zero,
            Vector3.forward * settings.HalfSize,
            Vector3.forward,
            Vector3.right,
            Vector3.forward,
            Vector3.right,
            0f,
            0f,
            out float targetOffset,
            out _,
            out _);

        Assert.That(targetOffset, Is.EqualTo(6f).Within(0.0001f));
    }

    private static RelativeHeightBrushSettings CreateSettings(HeightBrushShape shape)
    {
        return new RelativeHeightBrushSettings
        {
            Shape = shape,
            Size = 10f,
            Strength = 1f,
            FieldLength = 10f,
            FurrowSpacing = 1f,
            FurrowWidth = 1f,
            SingleFurrowLength = 10f,
            SingleFurrowWidth = 1f
        };
    }
}
}
