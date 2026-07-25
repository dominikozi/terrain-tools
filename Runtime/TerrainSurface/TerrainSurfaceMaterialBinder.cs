using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools
{
    internal static class TerrainSurfaceMaterialBinder
    {
        private const int MaximumLayerCount = TerrainSurfaceProfile.MaximumShaderLayerCapacity;

        private static readonly Vector4[] LayerTiling = new Vector4[MaximumLayerCount];
        private static readonly Vector4[] LayerHeightSurface = new Vector4[MaximumLayerCount];
        private static readonly Vector4[] LayerSurfaceExtra = new Vector4[MaximumLayerCount];
        private static readonly Vector4[] LayerAntiTiling = new Vector4[MaximumLayerCount];
        private static readonly Vector4[] LayerTriplanar = new Vector4[MaximumLayerCount];

        internal static void BindProfileProperties(
            MaterialPropertyBlock block,
            TerrainSurfaceProfile profile)
        {
            if (block == null || profile == null)
            {
                return;
            }

            block.SetTexture(TerrainSurfaceShaderIds.AlbedoHeightArray, profile.AlbedoHeightArray);
            block.SetTexture(TerrainSurfaceShaderIds.NormalSurfaceArray, profile.NormalSurfaceArray);
            block.SetTexture(TerrainSurfaceShaderIds.MetallicArray, profile.MetallicArray);
            block.SetFloat(TerrainSurfaceShaderIds.BlendQuality, (int)profile.BlendQuality);
            block.SetVector(
                TerrainSurfaceShaderIds.HeightParameters,
                new Vector4(profile.HeightTransition, profile.GlobalHeightOffset, profile.GlobalHeightContrast, 0f));

            FillLayerParameters(profile.Layers);
            block.SetVectorArray(TerrainSurfaceShaderIds.LayerTiling, LayerTiling);
            block.SetVectorArray(TerrainSurfaceShaderIds.LayerHeightSurface, LayerHeightSurface);
            block.SetVectorArray(TerrainSurfaceShaderIds.LayerSurfaceExtra, LayerSurfaceExtra);
            block.SetVectorArray(TerrainSurfaceShaderIds.LayerAntiTiling, LayerAntiTiling);
            block.SetVectorArray(TerrainSurfaceShaderIds.LayerTriplanar, LayerTriplanar);

            BindAntiTiling(block, profile.AntiTiling);
            BindStochasticSampling(block, profile.StochasticSampling);
            BindGlobalTexturing(block, profile.GlobalTexturing);
        }

        internal static void BindTerrain(
            Terrain terrain,
            Material material,
            int groupLayerCount,
            bool heightBlendEnabled,
            TerrainSurfaceProfile profile,
            MaterialPropertyBlock block)
        {
            if (terrain == null ||
                terrain.terrainData == null ||
                material == null ||
                profile == null ||
                block == null)
            {
                return;
            }

            TerrainData data = terrain.terrainData;
            terrain.materialTemplate = material;
            terrain.drawInstanced = true;

            terrain.GetSplatMaterialPropertyBlock(block);
            BindProfileProperties(block, profile);
            int controlCount = Mathf.Min(TerrainSurfaceShaderIds.Controls.Length, data.alphamapTextureCount);
            for (int i = 0; i < TerrainSurfaceShaderIds.Controls.Length; i++)
            {
                Texture control = i < controlCount ? data.GetAlphamapTexture(i) : Texture2D.blackTexture;
                block.SetTexture(TerrainSurfaceShaderIds.Controls[i], control);
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int alphamapWidth = Mathf.Max(1, data.alphamapWidth);
            int alphamapHeight = Mathf.Max(1, data.alphamapHeight);
            block.SetFloat(TerrainSurfaceShaderIds.ActiveLayerCount, Mathf.Min(groupLayerCount, MaximumLayerCount));
            block.SetFloat(TerrainSurfaceShaderIds.HeightBlend, heightBlendEnabled ? 1f : 0f);
            block.SetVector(TerrainSurfaceShaderIds.TerrainOriginSize, new Vector4(origin.x, origin.z, size.x, size.z));
            block.SetVector(TerrainSurfaceShaderIds.TerrainSizeY, new Vector4(origin.y, size.y, 0f, 0f));
            block.SetVector(
                TerrainSurfaceShaderIds.ControlTexelSize,
                new Vector4(1f / alphamapWidth, 1f / alphamapHeight, alphamapWidth, alphamapHeight));
            terrain.SetSplatMaterialPropertyBlock(block);
        }

        private static void FillLayerParameters(IReadOnlyList<TerrainSurfaceLayerSettings> layers)
        {
            for (int i = 0; i < MaximumLayerCount; i++)
            {
                TerrainSurfaceLayerSettings settings = i < layers.Count ? layers[i] : null;
                TerrainLayer layer = settings?.TerrainLayer;
                if (settings == null || layer == null)
                {
                    LayerTiling[i] = new Vector4(1f, 1f, 0f, 0f);
                    LayerHeightSurface[i] = new Vector4(0f, 1f, 1f, 1f);
                    LayerSurfaceExtra[i] = new Vector4(1f, 1f, 0f, 0f);
                    LayerAntiTiling[i] = Vector4.zero;
                    LayerTriplanar[i] = new Vector4(0f, 1f, 4f, 0.15f);
                    continue;
                }

                Vector2 tileSize = layer.tileSize;
                Vector2 tileOffset = layer.tileOffset;
                float reciprocalX = 1f / Mathf.Max(0.0001f, tileSize.x);
                float reciprocalY = 1f / Mathf.Max(0.0001f, tileSize.y);
                LayerTiling[i] = new Vector4(
                    reciprocalX,
                    reciprocalY,
                    tileOffset.x * reciprocalX,
                    tileOffset.y * reciprocalY);
                LayerHeightSurface[i] = new Vector4(
                    settings.HeightOffset,
                    settings.HeightContrast,
                    settings.MetallicMultiplier,
                    settings.NormalStrength);
                LayerSurfaceExtra[i] = new Vector4(
                    settings.SmoothnessMultiplier,
                    settings.AmbientOcclusionStrength,
                    settings.StochasticSampling ? 1f : 0f,
                    0f);
                LayerAntiTiling[i] = new Vector4(
                    settings.DetailNoiseStrength,
                    settings.MacroNoiseStrength,
                    settings.NormalNoiseStrength,
                    settings.DistanceResampleStrength);
                LayerTriplanar[i] = new Vector4(
                    settings.Triplanar ? 1f : 0f,
                    settings.TriplanarScale,
                    settings.TriplanarSharpness,
                    settings.TriplanarHeightTransition);
            }
        }

        private static void BindAntiTiling(
            MaterialPropertyBlock block,
            TerrainSurfaceAntiTilingSettings settings)
        {
            Vector4 flags = new Vector4(
                settings.DetailNoiseEnabled ? 1f : 0f,
                settings.MacroNoiseEnabled ? 1f : 0f,
                settings.NormalNoiseEnabled ? 1f : 0f,
                settings.DistanceResamplingEnabled ? 1f : 0f);
            block.SetVector(TerrainSurfaceShaderIds.AntiTilingFlags, flags);
            block.SetTexture(
                TerrainSurfaceShaderIds.DetailNoise,
                settings.DetailNoise != null ? settings.DetailNoise : Texture2D.grayTexture);
            block.SetVector(
                TerrainSurfaceShaderIds.DetailNoiseParameters,
                new Vector4(settings.DetailWorldScale, settings.DetailStrength, 0f, 0f));
            block.SetVector(TerrainSurfaceShaderIds.DetailNoiseFade, settings.DetailFade);
            block.SetTexture(
                TerrainSurfaceShaderIds.MacroNoise,
                settings.MacroNoise != null ? settings.MacroNoise : Texture2D.grayTexture);
            block.SetVector(
                TerrainSurfaceShaderIds.MacroNoiseParameters,
                new Vector4(settings.MacroWorldScale, settings.MacroStrength, 0f, 0f));
            block.SetVector(TerrainSurfaceShaderIds.MacroNoiseFade, settings.MacroFade);
            block.SetTexture(
                TerrainSurfaceShaderIds.NormalNoise,
                settings.NormalNoise != null ? settings.NormalNoise : Texture2D.normalTexture);
            block.SetVector(
                TerrainSurfaceShaderIds.NormalNoiseParameters,
                new Vector4(settings.NormalNoiseWorldScale, settings.NormalNoiseStrength, 0f, 0f));
            block.SetVector(TerrainSurfaceShaderIds.NormalNoiseFade, settings.NormalNoiseFade);
            block.SetVector(
                TerrainSurfaceShaderIds.DistanceResampleParameters,
                new Vector4(
                    settings.DistanceResampleScale,
                    settings.DistanceResampleStrength,
                    settings.DistanceResampleHeightBlend ? 1f : 0f,
                    0f));
            block.SetVector(TerrainSurfaceShaderIds.DistanceResampleFade, settings.DistanceResampleFade);
        }

        private static void BindStochasticSampling(
            MaterialPropertyBlock block,
            TerrainSurfaceStochasticSettings settings)
        {
            block.SetVector(
                TerrainSurfaceShaderIds.StochasticParameters,
                new Vector4(
                    settings.Enabled ? 1f : 0f,
                    settings.GridScale,
                    settings.BlendContrast,
                    settings.HeightTransition));
            block.SetVector(
                TerrainSurfaceShaderIds.StochasticExtra,
                new Vector4(
                    settings.Seed,
                    settings.RandomQuarterTurns ? 1f : 0f,
                    settings.HeightBlend ? 1f : 0f,
                    0f));
        }

        private static void BindGlobalTexturing(
            MaterialPropertyBlock block,
            TerrainSurfaceGlobalTexturingSettings settings)
        {
            Vector2 worldSize = settings.WorldSize;
            Vector2 replacementFade = settings.ReplacementFade;
            block.SetVector(
                TerrainSurfaceShaderIds.GlobalFlags,
                new Vector4(
                    settings.Enabled && settings.GlobalTint != null ? 1f : 0f,
                    settings.Enabled && settings.GlobalNormal != null ? 1f : 0f,
                    settings.ReplaceSplatInDistance ? 1f : 0f,
                    (int)settings.TintBlendMode));
            block.SetTexture(
                TerrainSurfaceShaderIds.GlobalTint,
                settings.GlobalTint != null ? settings.GlobalTint : Texture2D.grayTexture);
            block.SetTexture(
                TerrainSurfaceShaderIds.GlobalNormal,
                settings.GlobalNormal != null ? settings.GlobalNormal : Texture2D.normalTexture);
            block.SetVector(
                TerrainSurfaceShaderIds.GlobalMapping,
                new Vector4(
                    1f / worldSize.x,
                    1f / worldSize.y,
                    -settings.WorldOffset.x / worldSize.x,
                    -settings.WorldOffset.y / worldSize.y));
            block.SetVector(
                TerrainSurfaceShaderIds.GlobalParameters,
                new Vector4(settings.TintStrength, settings.NormalStrength, 0f, 0f));
            block.SetVector(TerrainSurfaceShaderIds.GlobalFade, settings.Fade);
            block.SetVector(TerrainSurfaceShaderIds.GlobalFadeOpacity, settings.FadeOpacity);
            block.SetVector(
                TerrainSurfaceShaderIds.GlobalReplacement,
                new Vector4(replacementFade.x, replacementFade.y, settings.ReplacementStrength, 0f));
        }
    }
}
