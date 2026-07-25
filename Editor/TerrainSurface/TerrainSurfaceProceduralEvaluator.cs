using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal sealed class TerrainSurfaceProceduralEvaluator : IDisposable
    {
        private readonly struct PreparedRule
        {
            internal readonly TerrainSurfaceProceduralRule Rule;
            internal readonly int LayerIndex;
            internal readonly int OriginalIndex;

            internal PreparedRule(TerrainSurfaceProceduralRule rule, int layerIndex, int originalIndex)
            {
                Rule = rule;
                LayerIndex = layerIndex;
                OriginalIndex = originalIndex;
            }
        }

        private readonly TerrainSurfaceProceduralProfile profile;
        private readonly Terrain[] terrains;
        private readonly PreparedRule[] rules;
        private readonly int fallbackLayerIndex;
        private readonly Dictionary<Texture2D, Texture2D> readableMasks = new();
        private readonly Color[] layerPreviewColors;

        internal int LayerCount => layerPreviewColors.Length;

        internal TerrainSurfaceProceduralEvaluator(
            TerrainSurfaceGroup group,
            TerrainSurfaceProceduralProfile proceduralProfile)
        {
            profile = proceduralProfile ?? throw new ArgumentNullException(nameof(proceduralProfile));
            terrains = new Terrain[group.Terrains.Count];
            for (int i = 0; i < group.Terrains.Count; i++)
            {
                terrains[i] = group.Terrains[i];
            }

            TerrainLayer[] layers = terrains[0].terrainData.terrainLayers;
            fallbackLayerIndex = FindLayerIndex(layers, profile.FallbackLayer);
            if (fallbackLayerIndex < 0)
            {
                fallbackLayerIndex = 0;
            }

            layerPreviewColors = new Color[layers.Length];
            for (int i = 0; i < layerPreviewColors.Length; i++)
            {
                layerPreviewColors[i] = profile.FallbackPreviewColor;
            }

            List<PreparedRule> prepared = new List<PreparedRule>();
            for (int i = 0; i < profile.Rules.Count; i++)
            {
                TerrainSurfaceProceduralRule rule = profile.Rules[i];
                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                int layerIndex = FindLayerIndex(layers, rule.TargetLayer);
                if (layerIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Rule '{rule.Label}' targets TerrainLayer '{rule.TargetLayer.name}', which is not in the terrain group.");
                }

                prepared.Add(new PreparedRule(rule, layerIndex, i));
                layerPreviewColors[layerIndex] = rule.PreviewColor;
                if (rule.RegionMaskEnabled && !readableMasks.ContainsKey(rule.RegionMask))
                {
                    readableMasks.Add(rule.RegionMask, CreateReadableCopy(rule.RegionMask));
                }
            }

            prepared.Sort((a, b) =>
            {
                int priority = b.Rule.Priority.CompareTo(a.Rule.Priority);
                return priority != 0 ? priority : a.OriginalIndex.CompareTo(b.OriginalIndex);
            });
            rules = prepared.ToArray();
        }

        public void Dispose()
        {
            foreach (Texture2D readable in readableMasks.Values)
            {
                if (readable != null)
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }
            }
            readableMasks.Clear();
        }

        internal float[,,] EvaluateTerrain(
            Terrain terrain,
            int width,
            int height,
            Func<float, bool> cancelRequested = null)
        {
            TerrainData data = terrain.terrainData;
            int layerCount = data.terrainLayers.Length;
            float[,,] result = new float[height, width, layerCount];
            float[] weights = new float[layerCount];
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;

            for (int y = 0; y < height; y++)
            {
                if ((y & 15) == 0 && cancelRequested != null && cancelRequested((float)y / height))
                {
                    throw new OperationCanceledException("Procedural terrain bake was cancelled.");
                }

                float v = (y + 0.5f) / height;
                float worldZ = origin.z + v * size.z;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float worldX = origin.x + u * size.x;
                    float worldHeight = origin.y + data.GetInterpolatedHeight(u, v);
                    float slope = data.GetSteepness(u, v);
                    Array.Clear(weights, 0, weights.Length);

                    float remaining = 1f;
                    for (int ruleIndex = 0; ruleIndex < rules.Length && remaining > 0.000001f; ruleIndex++)
                    {
                        PreparedRule prepared = rules[ruleIndex];
                        float score = EvaluateRule(
                            prepared.Rule,
                            terrain,
                            worldX,
                            worldZ,
                            worldHeight,
                            slope);
                        float claimed = Mathf.Clamp01(score) * remaining;
                        weights[prepared.LayerIndex] += claimed;
                        remaining -= claimed;
                    }

                    weights[fallbackLayerIndex] += remaining;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        result[y, x, layer] = weights[layer];
                    }
                }
            }

            if (cancelRequested != null && cancelRequested(1f))
            {
                throw new OperationCanceledException("Procedural terrain bake was cancelled.");
            }

            return result;
        }

        internal Color EvaluatePreviewColor(float[] weights)
        {
            Color color = Color.black;
            int count = Mathf.Min(weights.Length, layerPreviewColors.Length);
            for (int i = 0; i < count; i++)
            {
                color += layerPreviewColors[i] * weights[i];
            }
            color.a = 1f;
            return color;
        }

        private float EvaluateRule(
            TerrainSurfaceProceduralRule rule,
            Terrain preferredTerrain,
            float worldX,
            float worldZ,
            float height,
            float slope)
        {
            float score = rule.Strength;
            if (rule.HeightEnabled)
            {
                score *= SoftBand(height, rule.HeightRange, rule.HeightFalloff);
            }
            if (rule.SlopeEnabled)
            {
                score *= SoftBand(slope, rule.SlopeRange, rule.SlopeFalloff);
            }
            if (rule.CavityEnabled)
            {
                float radius = rule.CavityRadius;
                float neighborAverage =
                    (SampleWorldHeight(preferredTerrain, worldX - radius, worldZ) +
                     SampleWorldHeight(preferredTerrain, worldX + radius, worldZ) +
                     SampleWorldHeight(preferredTerrain, worldX, worldZ - radius) +
                     SampleWorldHeight(preferredTerrain, worldX, worldZ + radius)) * 0.25f;
                float cavity = Mathf.Clamp((neighborAverage - height) * rule.CavityScale, -1f, 1f);
                score *= SoftBand(cavity, rule.CavityRange, rule.CavityFalloff);
            }
            if (rule.NoiseEnabled)
            {
                float noise = FractalNoise(
                    worldX * rule.NoiseWorldScale,
                    worldZ * rule.NoiseWorldScale,
                    rule.NoiseOctaves,
                    rule.NoisePersistence,
                    rule.NoiseSeed);
                score *= Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        rule.NoiseThreshold - rule.NoiseTransition,
                        rule.NoiseThreshold + rule.NoiseTransition,
                        noise));
            }
            if (rule.RegionMaskEnabled)
            {
                Texture2D mask = readableMasks[rule.RegionMask];
                Vector2 size = rule.RegionWorldSize;
                Vector2 offset = rule.RegionWorldOffset;
                float u = (worldX - offset.x) / size.x;
                float v = (worldZ - offset.y) / size.y;
                Color sample = mask.GetPixelBilinear(u, v);
                float maskValue = rule.RegionMaskChannel switch
                {
                    TerrainSurfaceMaskChannel.Red => sample.r,
                    TerrainSurfaceMaskChannel.Green => sample.g,
                    TerrainSurfaceMaskChannel.Blue => sample.b,
                    _ => sample.a
                };
                score *= rule.InvertRegionMask ? 1f - maskValue : maskValue;
            }

            return score;
        }

        private float SampleWorldHeight(Terrain preferredTerrain, float worldX, float worldZ)
        {
            if (Contains(preferredTerrain, worldX, worldZ))
            {
                return SampleHeight(preferredTerrain, worldX, worldZ);
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain candidate = terrains[i];
                if (candidate != null && Contains(candidate, worldX, worldZ))
                {
                    return SampleHeight(candidate, worldX, worldZ);
                }
            }

            return SampleHeightClamped(preferredTerrain, worldX, worldZ);
        }

        private static bool Contains(Terrain terrain, float worldX, float worldZ)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return worldX >= origin.x && worldX <= origin.x + size.x &&
                   worldZ >= origin.z && worldZ <= origin.z + size.z;
        }

        private static float SampleHeight(Terrain terrain, float worldX, float worldZ)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float u = (worldX - origin.x) / size.x;
            float v = (worldZ - origin.z) / size.z;
            return origin.y + terrain.terrainData.GetInterpolatedHeight(u, v);
        }

        private static float SampleHeightClamped(Terrain terrain, float worldX, float worldZ)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float u = Mathf.Clamp01((worldX - origin.x) / size.x);
            float v = Mathf.Clamp01((worldZ - origin.z) / size.z);
            return origin.y + terrain.terrainData.GetInterpolatedHeight(u, v);
        }

        private static float SoftBand(float value, Vector2 range, float falloff)
        {
            if (falloff <= 0.000001f)
            {
                return value >= range.x && value <= range.y ? 1f : 0f;
            }

            float enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range.x - falloff, range.x, value));
            float exit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(range.y, range.y + falloff, value));
            return enter * exit;
        }

        private static float FractalNoise(float x, float y, int octaves, float persistence, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float normalization = 0f;
            float frequency = 1f;
            float seedX = (seed * 0.1031f) % 8192f;
            float seedY = (seed * 0.11369f) % 8192f;
            for (int octave = 0; octave < octaves; octave++)
            {
                sum += Mathf.PerlinNoise(x * frequency + seedX, y * frequency + seedY) * amplitude;
                normalization += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
                seedX += 17.17f;
                seedY += 31.73f;
            }
            return normalization > 0f ? sum / normalization : 0f;
        }

        private static int FindLayerIndex(TerrainLayer[] layers, TerrainLayer target)
        {
            if (target == null)
            {
                return -1;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == target)
                {
                    return i;
                }
            }
            return -1;
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                {
                    name = source.name + " (Terrain Surface Readable Copy)",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = source.wrapMode,
                    filterMode = FilterMode.Bilinear
                };
                copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                if (copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
