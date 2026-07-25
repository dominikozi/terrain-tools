using Dominikozi.TerrainTools.Editor.Painters;
using NUnit.Framework;

namespace Dominikozi.TerrainTools.Tests.Editor
{
public sealed class PainterMathTests
{
    [Test]
    public void CompositeBlend_NormalizesAndPreservesUnpaintedLayerRatio()
    {
        float[] current = { 0.1f, 0.2f, 0.3f, 0.4f };
        float[] target = { 0.75f, 0.25f, 0f, 0f };
        float[] result = new float[4];

        CompositeTerrainPaintMath.BlendNormalized(current, target, 0.5f, result);

        Assert.That(result[0] + result[1] + result[2] + result[3], Is.EqualTo(1f).Within(0.00001f));
        Assert.That(result[2] / result[3], Is.EqualTo(current[2] / current[3]).Within(0.00001f));
        Assert.That(result[0], Is.GreaterThan(current[0]));
    }

    [Test]
    public void CompositeNormalize_UsesEvenFallbackForZeroWeights()
    {
        float[] weights = { 0f, -1f, 0f, 0f };

        CompositeTerrainPaintMath.Normalize(weights);

        Assert.That(weights, Is.All.EqualTo(0.25f).Within(0.00001f));
    }

    [Test]
    public void DetailDither_IsDeterministicAndTracksFractionalDensity()
    {
        int first = DetailTerrainPaintUtility.DitherDetailCount(2.45f, 12, 19, 73);
        int second = DetailTerrainPaintUtility.DitherDetailCount(2.45f, 12, 19, 73);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Is.InRange(2, 3));
    }

    [Test]
    public void DetailPaintAmount_HandlesPaintEraseAndClamping()
    {
        Assert.That(
            DetailTerrainPaintUtility.ApplyPaintAmount(2, 4, 8, 0.5f, eraseMode: false),
            Is.EqualTo(4));
        Assert.That(
            DetailTerrainPaintUtility.ApplyPaintAmount(2, 4, 8, 1f, eraseMode: true),
            Is.Zero);
        Assert.That(
            DetailTerrainPaintUtility.ApplyPaintAmount(7, 4, 8, 1f, eraseMode: false),
            Is.EqualTo(8));
    }
}
}
