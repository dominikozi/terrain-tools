#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [Flags]
    internal enum TerrainDataTransferContent
    {
        None = 0,
        TerrainLayers = 1 << 0,
        DetailPrototypes = 1 << 1,
        TreePrototypes = 1 << 2,
        All = TerrainLayers | DetailPrototypes | TreePrototypes
    }

    internal readonly struct TerrainDataTransferResult
    {
        public TerrainDataTransferResult(
            int terrainCount,
            int terrainDataCount,
            int removedTerrainLayers,
            int removedDetailPrototypes,
            int removedTreeInstances)
        {
            TerrainCount = terrainCount;
            TerrainDataCount = terrainDataCount;
            RemovedTerrainLayers = removedTerrainLayers;
            RemovedDetailPrototypes = removedDetailPrototypes;
            RemovedTreeInstances = removedTreeInstances;
        }

        public int TerrainCount { get; }
        public int TerrainDataCount { get; }
        public int RemovedTerrainLayers { get; }
        public int RemovedDetailPrototypes { get; }
        public int RemovedTreeInstances { get; }
    }

    internal static class TerrainDataTransferService
    {
        private const string UndoName = "Transfer Terrain Prototypes And Layers";

        public static bool TryValidateSource(
            TerrainData source,
            TerrainDataTransferContent content,
            out string error)
        {
            if (source == null)
            {
                error = "Select a source Terrain with TerrainData.";
                return false;
            }

            if (content == TerrainDataTransferContent.None)
            {
                error = "Select at least one content type to transfer.";
                return false;
            }

            if ((content & TerrainDataTransferContent.TerrainLayers) != 0)
            {
                TerrainLayer[] layers = source.terrainLayers ?? Array.Empty<TerrainLayer>();
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null)
                    {
                        continue;
                    }

                    error = $"Source TerrainData has a missing Terrain Layer at index {i}.";
                    return false;
                }
            }

            if ((content & TerrainDataTransferContent.DetailPrototypes) != 0)
            {
                DetailPrototype[] details = source.detailPrototypes ?? Array.Empty<DetailPrototype>();
                for (int i = 0; i < details.Length; i++)
                {
                    if (HasValidDetailSource(details[i]))
                    {
                        continue;
                    }

                    error = $"Source TerrainData has a missing detail asset at prototype index {i}.";
                    return false;
                }
            }

            if ((content & TerrainDataTransferContent.TreePrototypes) != 0)
            {
                TreePrototype[] trees = source.treePrototypes ?? Array.Empty<TreePrototype>();
                for (int i = 0; i < trees.Length; i++)
                {
                    if (trees[i] != null && trees[i].prefab != null)
                    {
                        continue;
                    }

                    error = $"Source TerrainData has a missing tree prefab at prototype index {i}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static TerrainDataTransferResult Transfer(
            TerrainData source,
            IReadOnlyList<Terrain> targetTerrains,
            TerrainDataTransferContent content)
        {
            if (!TryValidateSource(source, content, out string error))
            {
                throw new InvalidOperationException(error);
            }

            List<TerrainData> targetData = CollectUniqueTargetData(
                source,
                targetTerrains,
                out int terrainCount);
            if (targetData.Count == 0)
            {
                throw new InvalidOperationException("Add at least one target Terrain with different TerrainData.");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            int removedTerrainLayers = 0;
            int removedDetailPrototypes = 0;
            int removedTreeInstances = 0;

            try
            {
                for (int i = 0; i < targetData.Count; i++)
                {
                    TerrainData target = targetData[i];
                    EditorUtility.DisplayProgressBar(
                        "Terrain Data Transfer",
                        $"Updating {target.name} ({i + 1}/{targetData.Count})",
                        (float)(i + 1) / targetData.Count);

                    Undo.RegisterCompleteObjectUndo(target, UndoName);

                    if ((content & TerrainDataTransferContent.TerrainLayers) != 0)
                    {
                        removedTerrainLayers += TransferTerrainLayers(source, target);
                    }

                    if ((content & TerrainDataTransferContent.DetailPrototypes) != 0)
                    {
                        removedDetailPrototypes += TransferDetailPrototypes(source, target);
                    }

                    if ((content & TerrainDataTransferContent.TreePrototypes) != 0)
                    {
                        removedTreeInstances += TransferTreePrototypes(source, target);
                    }

                    target.RefreshPrototypes();
                    EditorUtility.SetDirty(target);
                }

                FlushTerrains(targetTerrains, source);
                Undo.CollapseUndoOperations(undoGroup);
                SaveTargetData(targetData);
                SceneView.RepaintAll();
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return new TerrainDataTransferResult(
                terrainCount,
                targetData.Count,
                removedTerrainLayers,
                removedDetailPrototypes,
                removedTreeInstances);
        }

        private static int TransferTerrainLayers(TerrainData source, TerrainData target)
        {
            TerrainLayer[] sourceLayers = source.terrainLayers ?? Array.Empty<TerrainLayer>();
            TerrainLayer[] targetLayers = target.terrainLayers ?? Array.Empty<TerrainLayer>();
            int[] sourceToTarget = BuildTerrainLayerMap(sourceLayers, targetLayers);

            float[,,] targetAlphamaps = null;
            if (targetLayers.Length > 0
                && target.alphamapLayers > 0
                && target.alphamapWidth > 0
                && target.alphamapHeight > 0)
            {
                targetAlphamaps = target.GetAlphamaps(
                    0,
                    0,
                    target.alphamapWidth,
                    target.alphamapHeight);
            }

            target.terrainLayers = (TerrainLayer[])sourceLayers.Clone();

            if (sourceLayers.Length > 0
                && target.alphamapWidth > 0
                && target.alphamapHeight > 0)
            {
                float[,,] remapped = RemapAlphamaps(
                    targetAlphamaps,
                    target.alphamapWidth,
                    target.alphamapHeight,
                    sourceToTarget);
                target.SetAlphamaps(0, 0, remapped);
            }

            return targetLayers.Length - CountMappedEntries(sourceToTarget);
        }

        private static int TransferDetailPrototypes(TerrainData source, TerrainData target)
        {
            DetailPrototype[] sourcePrototypes =
                source.detailPrototypes ?? Array.Empty<DetailPrototype>();
            DetailPrototype[] targetPrototypes =
                target.detailPrototypes ?? Array.Empty<DetailPrototype>();
            int[] sourceToTarget = BuildDetailPrototypeMap(sourcePrototypes, targetPrototypes);

            int[][,] preservedLayers = new int[sourcePrototypes.Length][,];
            if (target.detailWidth > 0 && target.detailHeight > 0)
            {
                for (int sourceIndex = 0; sourceIndex < sourceToTarget.Length; sourceIndex++)
                {
                    int targetIndex = sourceToTarget[sourceIndex];
                    if (targetIndex < 0)
                    {
                        continue;
                    }

                    preservedLayers[sourceIndex] = target.GetDetailLayer(
                        0,
                        0,
                        target.detailWidth,
                        target.detailHeight,
                        targetIndex);
                }
            }

            target.detailPrototypes = CloneDetailPrototypes(sourcePrototypes);

            if (target.detailWidth > 0 && target.detailHeight > 0)
            {
                int[,] emptyLayer = null;
                for (int sourceIndex = 0; sourceIndex < sourcePrototypes.Length; sourceIndex++)
                {
                    int[,] layer = preservedLayers[sourceIndex];
                    if (layer == null)
                    {
                        emptyLayer ??= new int[target.detailHeight, target.detailWidth];
                        layer = emptyLayer;
                    }

                    target.SetDetailLayer(0, 0, sourceIndex, layer);
                }
            }

            return targetPrototypes.Length - CountMappedEntries(sourceToTarget);
        }

        private static int TransferTreePrototypes(TerrainData source, TerrainData target)
        {
            TreePrototype[] sourcePrototypes =
                source.treePrototypes ?? Array.Empty<TreePrototype>();
            TreePrototype[] targetPrototypes =
                target.treePrototypes ?? Array.Empty<TreePrototype>();
            int[] sourceToTarget = BuildTreePrototypeMap(sourcePrototypes, targetPrototypes);
            int[] targetToSource = InvertMap(sourceToTarget, targetPrototypes.Length);

            TreeInstance[] targetInstances = target.treeInstances ?? Array.Empty<TreeInstance>();
            List<TreeInstance> preservedInstances = new(targetInstances.Length);
            int removedInstances = 0;

            for (int i = 0; i < targetInstances.Length; i++)
            {
                TreeInstance instance = targetInstances[i];
                int oldIndex = instance.prototypeIndex;
                if (oldIndex < 0
                    || oldIndex >= targetToSource.Length
                    || targetToSource[oldIndex] < 0)
                {
                    removedInstances++;
                    continue;
                }

                instance.prototypeIndex = targetToSource[oldIndex];
                preservedInstances.Add(instance);
            }

            target.treeInstances = Array.Empty<TreeInstance>();
            target.treePrototypes = CloneTreePrototypes(sourcePrototypes);
            target.treeInstances = preservedInstances.ToArray();

            return removedInstances;
        }

        internal static float[,,] RemapAlphamaps(
            float[,,] source,
            int width,
            int height,
            IReadOnlyList<int> sourceToTarget)
        {
            int layerCount = sourceToTarget.Count;
            float[,,] result = new float[height, width, layerCount];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                    {
                        int oldLayerIndex = sourceToTarget[layerIndex];
                        if (source == null
                            || oldLayerIndex < 0
                            || oldLayerIndex >= source.GetLength(2))
                        {
                            continue;
                        }

                        float weight = source[y, x, oldLayerIndex];
                        result[y, x, layerIndex] = weight;
                        sum += weight;
                    }

                    if (sum <= Mathf.Epsilon)
                    {
                        result[y, x, 0] = 1f;
                        continue;
                    }

                    float inverseSum = 1f / sum;
                    for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                    {
                        result[y, x, layerIndex] *= inverseSum;
                    }
                }
            }

            return result;
        }

        internal static int[] BuildTerrainLayerMap(
            IReadOnlyList<TerrainLayer> source,
            IReadOnlyList<TerrainLayer> target)
        {
            return BuildOccurrenceMap(
                source.Count,
                target.Count,
                (sourceIndex, targetIndex) => source[sourceIndex] == target[targetIndex]);
        }

        internal static int[] BuildDetailPrototypeMap(
            IReadOnlyList<DetailPrototype> source,
            IReadOnlyList<DetailPrototype> target)
        {
            return BuildOccurrenceMap(
                source.Count,
                target.Count,
                (sourceIndex, targetIndex) =>
                    HasSameDetailSource(source[sourceIndex], target[targetIndex]));
        }

        internal static int[] BuildTreePrototypeMap(
            IReadOnlyList<TreePrototype> source,
            IReadOnlyList<TreePrototype> target)
        {
            return BuildOccurrenceMap(
                source.Count,
                target.Count,
                (sourceIndex, targetIndex) =>
                    source[sourceIndex].prefab == target[targetIndex].prefab);
        }

        private static int[] BuildOccurrenceMap(
            int sourceCount,
            int targetCount,
            Func<int, int, bool> matches)
        {
            int[] map = new int[sourceCount];
            bool[] usedTargetEntries = new bool[targetCount];

            for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                map[sourceIndex] = -1;
                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
                {
                    if (usedTargetEntries[targetIndex] || !matches(sourceIndex, targetIndex))
                    {
                        continue;
                    }

                    map[sourceIndex] = targetIndex;
                    usedTargetEntries[targetIndex] = true;
                    break;
                }
            }

            return map;
        }

        private static int[] InvertMap(IReadOnlyList<int> sourceToTarget, int targetCount)
        {
            int[] targetToSource = new int[targetCount];
            for (int i = 0; i < targetToSource.Length; i++)
            {
                targetToSource[i] = -1;
            }

            for (int sourceIndex = 0; sourceIndex < sourceToTarget.Count; sourceIndex++)
            {
                int targetIndex = sourceToTarget[sourceIndex];
                if (targetIndex >= 0)
                {
                    targetToSource[targetIndex] = sourceIndex;
                }
            }

            return targetToSource;
        }

        private static int CountMappedEntries(IReadOnlyList<int> sourceToTarget)
        {
            int count = 0;
            for (int i = 0; i < sourceToTarget.Count; i++)
            {
                if (sourceToTarget[i] >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasValidDetailSource(DetailPrototype prototype)
        {
            return prototype != null
                && (prototype.usePrototypeMesh
                    ? prototype.prototype != null
                    : prototype.prototypeTexture != null);
        }

        private static bool HasSameDetailSource(
            DetailPrototype left,
            DetailPrototype right)
        {
            if (left == null || right == null || left.usePrototypeMesh != right.usePrototypeMesh)
            {
                return false;
            }

            return left.usePrototypeMesh
                ? left.prototype == right.prototype
                : left.prototypeTexture == right.prototypeTexture;
        }

        private static TreePrototype[] CloneTreePrototypes(
            IReadOnlyList<TreePrototype> prototypes)
        {
            TreePrototype[] clones = new TreePrototype[prototypes.Count];
            for (int i = 0; i < prototypes.Count; i++)
            {
                TreePrototype prototype = prototypes[i];
                clones[i] = new TreePrototype
                {
                    prefab = prototype.prefab,
                    bendFactor = prototype.bendFactor,
                    navMeshLod = prototype.navMeshLod
                };
            }

            return clones;
        }

        private static DetailPrototype[] CloneDetailPrototypes(
            IReadOnlyList<DetailPrototype> prototypes)
        {
            DetailPrototype[] clones = new DetailPrototype[prototypes.Count];
            for (int i = 0; i < prototypes.Count; i++)
            {
                DetailPrototype prototype = prototypes[i];
                clones[i] = new DetailPrototype
                {
                    prototype = prototype.prototype,
                    prototypeTexture = prototype.prototypeTexture,
                    minWidth = prototype.minWidth,
                    maxWidth = prototype.maxWidth,
                    minHeight = prototype.minHeight,
                    maxHeight = prototype.maxHeight,
                    noiseSeed = prototype.noiseSeed,
                    noiseSpread = prototype.noiseSpread,
                    holeEdgePadding = prototype.holeEdgePadding,
                    density = prototype.density,
                    healthyColor = prototype.healthyColor,
                    dryColor = prototype.dryColor,
                    renderMode = prototype.renderMode,
                    usePrototypeMesh = prototype.usePrototypeMesh,
                    useInstancing = prototype.useInstancing,
                    useDensityScaling = prototype.useDensityScaling,
                    alignToGround = prototype.alignToGround,
                    positionJitter = prototype.positionJitter,
                    targetCoverage = prototype.targetCoverage
                };
            }

            return clones;
        }

        private static List<TerrainData> CollectUniqueTargetData(
            TerrainData source,
            IReadOnlyList<Terrain> targetTerrains,
            out int terrainCount)
        {
            List<TerrainData> result = new();
            HashSet<Terrain> seenTerrains = new();
            HashSet<TerrainData> seen = new();
            terrainCount = 0;

            if (targetTerrains == null)
            {
                return result;
            }

            for (int i = 0; i < targetTerrains.Count; i++)
            {
                Terrain terrain = targetTerrains[i];
                if (terrain == null
                    || terrain.terrainData == null
                    || !terrain.gameObject.scene.IsValid()
                    || terrain.terrainData == source
                    || !seenTerrains.Add(terrain))
                {
                    continue;
                }

                terrainCount++;
                if (seen.Add(terrain.terrainData))
                {
                    result.Add(terrain.terrainData);
                }
            }

            return result;
        }

        private static void FlushTerrains(
            IReadOnlyList<Terrain> terrains,
            TerrainData source)
        {
            if (terrains == null)
            {
                return;
            }

            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain != null
                    && terrain.terrainData != null
                    && terrain.gameObject.scene.IsValid()
                    && terrain.terrainData != source)
                {
                    terrain.Flush();
                }
            }
        }

        private static void SaveTargetData(IReadOnlyList<TerrainData> targetData)
        {
            for (int i = 0; i < targetData.Count; i++)
            {
                AssetDatabase.SaveAssetIfDirty(targetData[i]);
            }
        }
    }
}
#endif
