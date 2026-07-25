using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainSurfaceProceduralBakeService
    {
        private sealed class PendingBake
        {
            internal Terrain Terrain;
            internal int Width;
            internal int Height;
            internal int LayerCount;
            internal string TemporaryPath;
        }

        internal static bool TryValidate(
            TerrainSurfaceGroup group,
            TerrainSurfaceProceduralProfile profile,
            out Terrain firstTerrain,
            out string error)
        {
            firstTerrain = null;
            if (group == null)
            {
                error = "Assign a TerrainSurfaceGroup.";
                return false;
            }
            if (profile == null)
            {
                error = "Assign or create a TerrainSurfaceProceduralProfile.";
                return false;
            }

            group.Synchronize();
            if (group.Terrains.Count == 0)
            {
                error = "The terrain group contains no terrains.";
                return false;
            }

            firstTerrain = group.Terrains[0];
            if (firstTerrain == null || firstTerrain.terrainData == null)
            {
                error = "The first terrain or its TerrainData is missing.";
                return false;
            }

            TerrainLayer[] canonicalLayers = firstTerrain.terrainData.terrainLayers;
            if (canonicalLayers.Length == 0 || canonicalLayers.Length > TerrainSurfaceProfile.MaximumShaderLayerCapacity)
            {
                error = $"Terrain layer count must be between 1 and {TerrainSurfaceProfile.MaximumShaderLayerCapacity}.";
                return false;
            }

            for (int terrainIndex = 0; terrainIndex < group.Terrains.Count; terrainIndex++)
            {
                Terrain terrain = group.Terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                {
                    error = $"Terrain entry {terrainIndex} is missing.";
                    return false;
                }

                TerrainLayer[] layers = terrain.terrainData.terrainLayers;
                if (layers.Length != canonicalLayers.Length)
                {
                    error = $"Terrain '{terrain.name}' has a different layer count.";
                    return false;
                }
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    if (layers[layerIndex] != canonicalLayers[layerIndex])
                    {
                        error = $"Terrain '{terrain.name}' has a different layer order at index {layerIndex}.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        internal static TerrainSurfaceAlphamapBackup BakeAll(
            TerrainSurfaceGroup group,
            TerrainSurfaceProceduralProfile profile)
        {
            if (!TryValidate(group, profile, out _, out string error))
            {
                throw new InvalidOperationException(error);
            }

            string temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "TerrainToolsSurfaceBake_" + Guid.NewGuid().ToString("N"));
            List<PendingBake> pending = new List<PendingBake>();
            TerrainSurfaceAlphamapBackup backup = null;
            try
            {
                Directory.CreateDirectory(temporaryRoot);
                using (TerrainSurfaceProceduralEvaluator evaluator =
                       new TerrainSurfaceProceduralEvaluator(group, profile))
                {
                    for (int terrainIndex = 0; terrainIndex < group.Terrains.Count; terrainIndex++)
                    {
                        Terrain terrain = group.Terrains[terrainIndex];
                        TerrainData data = terrain.terrainData;
                        int capturedIndex = terrainIndex;
                        float[,,] generated = evaluator.EvaluateTerrain(
                            terrain,
                            data.alphamapWidth,
                            data.alphamapHeight,
                            tileProgress => EditorUtility.DisplayCancelableProgressBar(
                                "Computing Procedural Terrain Textures",
                                $"{terrain.name} ({capturedIndex + 1}/{group.Terrains.Count})",
                                (capturedIndex + tileProgress) / group.Terrains.Count));
                        string temporaryPath = Path.Combine(temporaryRoot, $"tile_{terrainIndex}.bytes.gz");
                        TerrainSurfaceAlphamapCodec.WriteToFile(temporaryPath, generated);
                        pending.Add(new PendingBake
                        {
                            Terrain = terrain,
                            Width = data.alphamapWidth,
                            Height = data.alphamapHeight,
                            LayerCount = data.alphamapLayers,
                            TemporaryPath = temporaryPath
                        });
                    }
                }

                backup = CreateBackup(group, profile);
                try
                {
                    ApplyPending(pending);
                }
                catch (Exception applyException)
                {
                    try
                    {
                        RestoreBackup(backup);
                    }
                    catch (Exception restoreException)
                    {
                        throw new AggregateException(
                            $"Bake failed and automatic restore also failed. Backup: {AssetDatabase.GetAssetPath(backup)}",
                            applyException,
                            restoreException);
                    }

                    throw new InvalidOperationException(
                        $"Bake failed and the automatic backup was restored. Error: {applyException.Message}",
                        applyException);
                }

                group.Synchronize();
                AssetDatabase.SaveAssets();
                return backup;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
            }
        }

        internal static void RestoreBackup(TerrainSurfaceAlphamapBackup backup)
        {
            if (backup == null)
            {
                throw new ArgumentNullException(nameof(backup));
            }

            for (int i = 0; i < backup.Entries.Count; i++)
            {
                TerrainSurfaceAlphamapBackupEntry entry = backup.Entries[i];
                TerrainData data = entry.TerrainData;
                if (data == null)
                {
                    throw new InvalidOperationException($"Backup entry {i} no longer references TerrainData.");
                }
                if (data.alphamapWidth != entry.Width || data.alphamapHeight != entry.Height ||
                    data.alphamapLayers != entry.LayerCount)
                {
                    throw new InvalidOperationException(
                        $"TerrainData '{data.name}' dimensions or layer count changed since the backup was created.");
                }

                EditorUtility.DisplayProgressBar(
                    "Restoring Terrain Alphamaps",
                    data.name,
                    (float)i / backup.Entries.Count);
                float[,,] alphamaps = TerrainSurfaceAlphamapCodec.Decode(
                    entry.CompressedWeights,
                    entry.Width,
                    entry.Height,
                    entry.LayerCount);
                data.SetAlphamaps(0, 0, alphamaps);
                EditorUtility.SetDirty(data);
            }
        }

        private static TerrainSurfaceAlphamapBackup CreateBackup(
            TerrainSurfaceGroup group,
            UnityEngine.Object assetContext)
        {
            List<TerrainSurfaceAlphamapBackupEntry> entries = new List<TerrainSurfaceAlphamapBackupEntry>();
            for (int i = 0; i < group.Terrains.Count; i++)
            {
                TerrainData data = group.Terrains[i].terrainData;
                EditorUtility.DisplayProgressBar(
                    "Backing Up Terrain Alphamaps",
                    group.Terrains[i].name,
                    (float)i / group.Terrains.Count);
                float[,,] alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                entries.Add(new TerrainSurfaceAlphamapBackupEntry(
                    data,
                    data.alphamapWidth,
                    data.alphamapHeight,
                    data.alphamapLayers,
                    TerrainSurfaceAlphamapCodec.Encode(alphamaps)));
            }

            string folder = GetAssetFolder(assetContext);
            TerrainToolsPaths.EnsureAssetFolder(folder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{assetContext.name}_Alphamaps_{timestamp}.asset");
            TerrainSurfaceAlphamapBackup backup = ScriptableObject.CreateInstance<TerrainSurfaceAlphamapBackup>();
            backup.Initialize(entries);
            AssetDatabase.CreateAsset(backup, path);
            EditorUtility.SetDirty(backup);
            AssetDatabase.SaveAssets();
            return backup;
        }

        private static void ApplyPending(IReadOnlyList<PendingBake> pending)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PendingBake item = pending[i];
                EditorUtility.DisplayProgressBar(
                    "Applying Procedural Terrain Textures",
                    item.Terrain.name,
                    (float)i / pending.Count);
                float[,,] alphamaps = TerrainSurfaceAlphamapCodec.ReadFromFile(
                    item.TemporaryPath,
                    item.Width,
                    item.Height,
                    item.LayerCount);
                item.Terrain.terrainData.SetAlphamaps(0, 0, alphamaps);
                EditorUtility.SetDirty(item.Terrain.terrainData);
            }
        }

        private static string GetAssetFolder(UnityEngine.Object context)
        {
            string path = AssetDatabase.GetAssetPath(context);
            if (string.IsNullOrWhiteSpace(path))
            {
                return TerrainToolsPaths.TerrainSurfaceGeneratedRoot;
            }
            return Path.GetDirectoryName(path)?.Replace('\\', '/')
                   ?? TerrainToolsPaths.TerrainSurfaceGeneratedRoot;
        }
    }
}
