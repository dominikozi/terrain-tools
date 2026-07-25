#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal static class CompositeTerrainPaintMath
{
    public static void BlendNormalized(
        float[] currentWeights,
        float[] targetWeights,
        float opacity,
        float[] destination)
    {
        if (currentWeights == null ||
            targetWeights == null ||
            destination == null ||
            currentWeights.Length != targetWeights.Length ||
            currentWeights.Length != destination.Length)
        {
            throw new ArgumentException("Composite weight arrays must be non-null and have matching lengths.");
        }

        float clampedOpacity = Mathf.Clamp01(opacity);
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Mathf.Lerp(
                Mathf.Max(0f, currentWeights[i]),
                Mathf.Max(0f, targetWeights[i]),
                clampedOpacity);
        }

        Normalize(destination);
    }

    public static void Normalize(float[] weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = Mathf.Max(0f, weights[i]);
            total += weights[i];
        }

        if (total <= 0.000001f)
        {
            float fallback = weights.Length > 0 ? 1f / weights.Length : 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = fallback;
            }

            return;
        }

        float inverseTotal = 1f / total;
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] *= inverseTotal;
        }
    }
}
}
#endif
