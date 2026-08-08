using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal static class TerrainBoundaryWeightUtility
    {
        public static void FindTopTwo(
            float[,,] weights,
            int y,
            int x,
            out int firstIndex,
            out float firstWeight,
            out int secondIndex,
            out float secondWeight)
        {
            firstIndex = -1;
            secondIndex = -1;
            firstWeight = float.NegativeInfinity;
            secondWeight = float.NegativeInfinity;
            int layerCount = weights.GetLength(2);
            for (int layer = 0; layer < layerCount; layer++)
            {
                float value = weights[y, x, layer];
                if (value > firstWeight)
                {
                    secondIndex = firstIndex;
                    secondWeight = firstWeight;
                    firstIndex = layer;
                    firstWeight = value;
                }
                else if (value > secondWeight)
                {
                    secondIndex = layer;
                    secondWeight = value;
                }
            }

            firstWeight = Mathf.Max(0f, firstWeight);
            secondWeight = Mathf.Max(0f, secondWeight);
        }

        public static void FindTopTwo(
            float[] weights,
            out int firstIndex,
            out float firstWeight,
            out int secondIndex,
            out float secondWeight)
        {
            firstIndex = -1;
            secondIndex = -1;
            firstWeight = float.NegativeInfinity;
            secondWeight = float.NegativeInfinity;
            for (int layer = 0; layer < weights.Length; layer++)
            {
                float value = weights[layer];
                if (value > firstWeight)
                {
                    secondIndex = firstIndex;
                    secondWeight = firstWeight;
                    firstIndex = layer;
                    firstWeight = value;
                }
                else if (value > secondWeight)
                {
                    secondIndex = layer;
                    secondWeight = value;
                }
            }

            firstWeight = Mathf.Max(0f, firstWeight);
            secondWeight = Mathf.Max(0f, secondWeight);
        }

        public static int FindDominant(float[,,] weights, int y, int x)
        {
            FindTopTwo(weights, y, x, out int first, out _, out _, out _);
            return first;
        }

        public static void CopyPixel(float[,,] source, int y, int x, float[] destination)
        {
            int count = Mathf.Min(source.GetLength(2), destination.Length);
            for (int layer = 0; layer < count; layer++)
            {
                destination[layer] = source[y, x, layer];
            }
        }

        public static void WritePixel(float[,,] destination, int y, int x, float[] source)
        {
            int count = Mathf.Min(destination.GetLength(2), source.Length);
            for (int layer = 0; layer < count; layer++)
            {
                destination[y, x, layer] = source[layer];
            }
        }

        public static void BlendPixel(
            float[,,] original,
            float[,,] destination,
            int y,
            int x,
            float[] target,
            float blend)
        {
            int layerCount = destination.GetLength(2);
            float clampedBlend = Mathf.Clamp01(blend);
            for (int layer = 0; layer < layerCount; layer++)
            {
                destination[y, x, layer] = Mathf.Lerp(
                    original[y, x, layer],
                    target[layer],
                    clampedBlend);
            }

            Normalize(destination, y, x);
        }

        public static void SetPairRatio(float[] weights, int layerA, int layerB, float ratioA)
        {
            if (layerA < 0 || layerB < 0 || layerA >= weights.Length || layerB >= weights.Length)
            {
                return;
            }

            float total = Mathf.Max(0f, weights[layerA]) + Mathf.Max(0f, weights[layerB]);
            float ratio = Mathf.Clamp01(ratioA);
            weights[layerA] = total * ratio;
            weights[layerB] = total * (1f - ratio);
        }

        public static void ApplyPairContrast(float[] weights, int layerA, int layerB, float contrast)
        {
            if (layerA < 0 || layerB < 0 || layerA >= weights.Length || layerB >= weights.Length)
            {
                return;
            }

            float a = Mathf.Max(0f, weights[layerA]);
            float b = Mathf.Max(0f, weights[layerB]);
            float total = a + b;
            if (total <= 0.000001f || contrast <= 0f)
            {
                return;
            }

            float exponent = Mathf.Lerp(1f, 8f, Mathf.Clamp01(contrast));
            float poweredA = Mathf.Pow(a / total, exponent);
            float poweredB = Mathf.Pow(b / total, exponent);
            float poweredTotal = poweredA + poweredB;
            if (poweredTotal <= 0.000001f)
            {
                return;
            }

            weights[layerA] = total * poweredA / poweredTotal;
            weights[layerB] = total * poweredB / poweredTotal;
        }

        public static void Normalize(float[] weights)
        {
            float total = 0f;
            for (int layer = 0; layer < weights.Length; layer++)
            {
                weights[layer] = Mathf.Max(0f, weights[layer]);
                total += weights[layer];
            }

            if (total <= 0.000001f)
            {
                if (weights.Length > 0)
                {
                    weights[0] = 1f;
                }
                return;
            }

            for (int layer = 0; layer < weights.Length; layer++)
            {
                weights[layer] /= total;
            }
        }

        public static void Normalize(float[,,] weights, int y, int x)
        {
            float total = 0f;
            int layerCount = weights.GetLength(2);
            for (int layer = 0; layer < layerCount; layer++)
            {
                weights[y, x, layer] = Mathf.Max(0f, weights[y, x, layer]);
                total += weights[y, x, layer];
            }

            if (total <= 0.000001f)
            {
                weights[y, x, 0] = 1f;
                for (int layer = 1; layer < layerCount; layer++)
                {
                    weights[y, x, layer] = 0f;
                }
                return;
            }

            for (int layer = 0; layer < layerCount; layer++)
            {
                weights[y, x, layer] /= total;
            }
        }

        public static bool PixelsDiffer(float[,,] first, float[,,] second, int y, int x, float epsilon)
        {
            int layerCount = first.GetLength(2);
            for (int layer = 0; layer < layerCount; layer++)
            {
                if (Mathf.Abs(first[y, x, layer] - second[y, x, layer]) > epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
