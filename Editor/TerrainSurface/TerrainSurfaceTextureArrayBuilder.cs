using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainSurfaceTextureArrayBuilder
    {
        private const string AlbedoHeightSuffix = "_AlbedoHeight.asset";
        private const string NormalSurfaceSuffix = "_NormalSurface.asset";
        private const string MetallicSuffix = "_Metallic.asset";

        internal static bool SynchronizeProfileLayers(TerrainSurfaceGroup group, out string message)
        {
            if (!TryGetBuildContext(group, out TerrainSurfaceProfile profile, out TerrainLayer[] layers, out message))
            {
                return false;
            }

            Undo.RecordObject(profile, "Synchronize Terrain Surface Layers");
            profile.SynchronizeLayers(layers);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            message = $"Synchronized {layers.Length} TerrainLayer(s) from the terrain group.";
            return true;
        }

        internal static bool Build(TerrainSurfaceGroup group, out string message)
        {
            if (!TryGetBuildContext(group, out TerrainSurfaceProfile profile, out TerrainLayer[] layers, out message))
            {
                return false;
            }

            Shader packingShader = Shader.Find(TerrainToolsAssetLocator.PackingShaderName);
            if (packingShader == null)
            {
                message = $"Required packing shader '{TerrainToolsAssetLocator.PackingShaderName}' was not found.";
                return false;
            }

            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                message = "Save the TerrainSurfaceProfile as an asset before building texture arrays.";
                return false;
            }

            int resolution = profile.TextureResolution;
            Material packingMaterial = null;
            Texture2DArray albedoHeight = null;
            Texture2DArray normalSurface = null;
            Texture2DArray metallic = null;
            try
            {
                packingMaterial = new Material(packingShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                albedoHeight = CreateCompressedArray(
                    resolution,
                    layers.Length,
                    TextureFormat.BC7,
                    linear: false,
                    profile.TextureMipBias,
                    "Terrain Albedo + Height");
                normalSurface = CreateCompressedArray(
                    resolution,
                    layers.Length,
                    TextureFormat.BC7,
                    linear: true,
                    profile.TextureMipBias,
                    "Terrain Normal + AO + Smoothness");
                metallic = CreateCompressedArray(
                    resolution,
                    layers.Length,
                    TextureFormat.BC4,
                    linear: true,
                    profile.TextureMipBias,
                    "Terrain Metallic");

                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    TerrainLayer layer = layers[layerIndex];
                    EditorUtility.DisplayProgressBar(
                        "Building Terrain Surface Arrays",
                        $"Packing layer {layerIndex + 1}/{layers.Length}: {layer.name}",
                        (float)layerIndex / layers.Length);

                    ConfigurePackingMaterial(packingMaterial, layer);
                    BakeSlice(packingMaterial, pass: 0, albedoHeight, layerIndex, resolution, linear: false);
                    BakeSlice(packingMaterial, pass: 1, normalSurface, layerIndex, resolution, linear: true);
                    BakeSlice(packingMaterial, pass: 2, metallic, layerIndex, resolution, linear: true);
                }

                albedoHeight.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                normalSurface.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                metallic.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                string directory = Path.GetDirectoryName(profilePath)?.Replace('\\', '/');
                string baseName = Path.GetFileNameWithoutExtension(profilePath);
                string albedoPath = $"{directory}/{baseName}{AlbedoHeightSuffix}";
                string normalPath = $"{directory}/{baseName}{NormalSurfaceSuffix}";
                string metallicPath = $"{directory}/{baseName}{MetallicSuffix}";
                Texture2DArray savedAlbedo = SaveOrReplaceArray(albedoHeight, albedoPath);
                Texture2DArray savedNormal = SaveOrReplaceArray(normalSurface, normalPath);
                Texture2DArray savedMetallic = SaveOrReplaceArray(metallic, metallicPath);
                albedoHeight = null;
                normalSurface = null;
                metallic = null;

                Undo.RecordObject(profile, "Assign Terrain Surface Texture Arrays");
                profile.SynchronizeLayers(layers);
                profile.AssignGeneratedArrays(savedAlbedo, savedNormal, savedMetallic);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                group.Synchronize();
                EditorUtility.SetDirty(group);
                message =
                    $"Built two {resolution}x{resolution} BC7 arrays and one BC4 metallic array " +
                    $"with {layers.Length} slice(s) and mipmaps.";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Texture array build failed: {exception.Message}";
                Debug.LogException(exception, group);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (packingMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(packingMaterial);
                }

                if (albedoHeight != null)
                {
                    UnityEngine.Object.DestroyImmediate(albedoHeight);
                }

                if (normalSurface != null)
                {
                    UnityEngine.Object.DestroyImmediate(normalSurface);
                }

                if (metallic != null)
                {
                    UnityEngine.Object.DestroyImmediate(metallic);
                }
            }
        }

        private static bool TryGetBuildContext(
            TerrainSurfaceGroup group,
            out TerrainSurfaceProfile profile,
            out TerrainLayer[] layers,
            out string message)
        {
            profile = null;
            layers = null;
            if (group == null)
            {
                message = "TerrainSurfaceGroup is missing.";
                return false;
            }

            group.Synchronize();
            profile = group.Profile;
            if (profile == null)
            {
                message = "Assign or create a TerrainSurfaceProfile first.";
                return false;
            }

            Terrain sourceTerrain = null;
            int maximumLayerCount = 0;
            for (int i = 0; i < group.Terrains.Count; i++)
            {
                Terrain terrain = group.Terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                int count = terrain.terrainData.terrainLayers.Length;
                if (count > maximumLayerCount)
                {
                    maximumLayerCount = count;
                    sourceTerrain = terrain;
                }
            }

            if (sourceTerrain == null || maximumLayerCount == 0)
            {
                message = "The terrain group has no TerrainLayers.";
                return false;
            }

            if (maximumLayerCount > TerrainSurfaceProfile.MaximumShaderLayerCapacity)
            {
                message =
                    $"The terrain group has {maximumLayerCount} layers; the shader supports at most " +
                    $"{TerrainSurfaceProfile.MaximumShaderLayerCapacity}.";
                return false;
            }

            layers = sourceTerrain.terrainData.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null)
                {
                    message = $"TerrainLayer at index {i} on '{sourceTerrain.name}' is missing.";
                    return false;
                }
            }

            for (int terrainIndex = 0; terrainIndex < group.Terrains.Count; terrainIndex++)
            {
                Terrain terrain = group.Terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                TerrainLayer[] candidateLayers = terrain.terrainData.terrainLayers;
                if (candidateLayers.Length != layers.Length)
                {
                    message =
                        $"Terrain '{terrain.name}' has {candidateLayers.Length} layers while '{sourceTerrain.name}' " +
                        $"has {layers.Length}. All tiles in one group must share the same ordered layer set.";
                    return false;
                }

                for (int layerIndex = 0; layerIndex < candidateLayers.Length; layerIndex++)
                {
                    if (layerIndex < layers.Length && candidateLayers[layerIndex] == layers[layerIndex])
                    {
                        continue;
                    }

                    message =
                        $"Terrain '{terrain.name}' does not use the same TerrainLayer order as '{sourceTerrain.name}' " +
                        $"at index {layerIndex}. All tiles in one group must share the same ordered layer set.";
                    return false;
                }
            }

            message = null;
            return true;
        }

        private static Texture2DArray CreateCompressedArray(
            int resolution,
            int depth,
            TextureFormat format,
            bool linear,
            float mipBias,
            string name)
        {
            Texture2DArray array = new Texture2DArray(
                resolution,
                resolution,
                depth,
                format,
                mipChain: true,
                linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                // TerrainLayer source textures in URP use bilinear filtering. The
                // shader binds the array's own sampler so this also preserves anisotropy.
                filterMode = FilterMode.Bilinear,
                anisoLevel = 8,
                mipMapBias = mipBias
            };
            return array;
        }

        private static void ConfigurePackingMaterial(Material material, TerrainLayer layer)
        {
            material.SetTexture("_SourceAlbedo", layer.diffuseTexture != null ? layer.diffuseTexture : Texture2D.grayTexture);
            material.SetTexture("_SourceNormal", layer.normalMapTexture != null ? layer.normalMapTexture : Texture2D.normalTexture);
            material.SetTexture("_SourceMask", layer.maskMapTexture != null ? layer.maskMapTexture : Texture2D.whiteTexture);
            material.SetFloat("_HasNormal", layer.normalMapTexture != null ? 1f : 0f);
            material.SetFloat("_HasMask", layer.maskMapTexture != null ? 1f : 0f);
            material.SetVector("_MaskRemapMin", layer.maskMapRemapMin);
            material.SetVector("_MaskRemapMax", layer.maskMapRemapMax);
            material.SetVector("_DiffuseRemapMin", layer.diffuseRemapMin);
            material.SetVector("_DiffuseRemapMax", layer.diffuseRemapMax);
            material.SetFloat(
                "_DefaultHeight",
                Mathf.Lerp(layer.maskMapRemapMin.z, layer.maskMapRemapMax.z, 0.5f));
            material.SetFloat("_DefaultMetallic", layer.metallic);
            material.SetFloat("_DefaultOcclusion", layer.maskMapRemapMax.y);
            material.SetFloat("_DefaultSmoothness", layer.smoothness);
        }

        private static void BakeSlice(
            Material material,
            int pass,
            Texture2DArray destination,
            int destinationSlice,
            int resolution,
            bool linear)
        {
            RenderTexture temporaryTarget = RenderTexture.GetTemporary(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D slice = null;
            try
            {
                Graphics.Blit(Texture2D.whiteTexture, temporaryTarget, material, pass);
                RenderTexture.active = temporaryTarget;
                slice = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: true, linear)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                slice.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0, recalculateMipMaps: false);
                slice.Apply(updateMipmaps: true, makeNoLongerReadable: false);
                EditorUtility.CompressTexture(slice, destination.format, TextureCompressionQuality.Best);

                int mipCount = Mathf.Min(slice.mipmapCount, destination.mipmapCount);
                for (int mip = 0; mip < mipCount; mip++)
                {
                    destination.SetPixelData(slice.GetPixelData<byte>(mip), mip, destinationSlice);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporaryTarget);
                if (slice != null)
                {
                    UnityEngine.Object.DestroyImmediate(slice);
                }
            }
        }

        private static Texture2DArray SaveOrReplaceArray(Texture2DArray generated, string path)
        {
            Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing == null)
            {
                generated.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            generated.name = existing.name;
            EditorUtility.CopySerialized(generated, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);
            return existing;
        }
    }
}
