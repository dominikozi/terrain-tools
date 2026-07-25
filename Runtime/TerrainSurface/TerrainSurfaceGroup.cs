using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dominikozi.TerrainTools
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Terrain Tools/Terrain Surface Group")]
    /// <summary>
    /// Represents the Terrain Surface Group runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceGroup : MonoBehaviour
    {
        [SerializeField] private TerrainSurfaceProfile profile;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material overLimitFallbackMaterial;
        [SerializeField] private bool collectTerrainsFromChildren = true;
        [SerializeField] private List<Terrain> terrains = new();
        [SerializeField, HideInInspector] private string validationMessage;

        private MaterialPropertyBlock terrainPropertyBlock;
        private int activeLayerCount;
        private bool renderBindingReady;

        /// <summary>
        /// Gets the configured Profile value.
        /// </summary>
        public TerrainSurfaceProfile Profile => profile;
        /// <summary>
        /// Gets the configured Terrain Material value.
        /// </summary>
        public Material TerrainMaterial => terrainMaterial;
        /// <summary>
        /// Gets the configured Terrains value.
        /// </summary>
        public IReadOnlyList<Terrain> Terrains => terrains;
        /// <summary>
        /// Gets the configured Validation Message value.
        /// </summary>
        public string ValidationMessage => validationMessage;

        private void OnEnable()
        {
            terrainPropertyBlock ??= new MaterialPropertyBlock();
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            Synchronize();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        }

        private void OnValidate()
        {
            Synchronize();
        }

        [ContextMenu("Synchronize Terrain Surface")]
        /// <summary>
        /// Executes the Synchronize operation.
        /// </summary>
        public void Synchronize()
        {
            renderBindingReady = false;
            if (collectTerrainsFromChildren)
            {
                CollectTerrainsFromChildren();
            }

            RemoveMissingTerrains();
            if (profile == null)
            {
                validationMessage = "Assign a TerrainSurfaceProfile.";
                return;
            }

            if (terrains.Count == 0)
            {
                validationMessage = "The group contains no Terrains.";
                return;
            }

            int maximumLayerCount = GetMaximumLayerCount();
            if (maximumLayerCount > TerrainSurfaceProfile.MaximumShaderLayerCapacity)
            {
                validationMessage =
                    $"The group uses {maximumLayerCount} TerrainLayers. Height blending supports at most " +
                    $"{TerrainSurfaceProfile.MaximumShaderLayerCapacity}; the fallback material is active.";
                ApplyFallbackMaterial();
                return;
            }

            if (terrainMaterial == null)
            {
                validationMessage = "Assign the generated Terrain Surface material.";
                return;
            }

            if (!profile.HasGeneratedArrays)
            {
                validationMessage = "Build the profile texture arrays before assigning the material to Terrains.";
                return;
            }

            if (!TryValidateLayerOrder(out string layerOrderError))
            {
                validationMessage = layerOrderError;
                return;
            }

            activeLayerCount = maximumLayerCount;
            renderBindingReady = true;
            ApplyRenderBindings();

            validationMessage =
                $"Ready: {terrains.Count} terrain tile(s), {maximumLayerCount} layer(s), " +
                $"{profile.BlendQuality}.";
        }

        /// <summary>
        /// Executes the Set Generated Setup operation.
        /// </summary>
        public void SetGeneratedSetup(TerrainSurfaceProfile newProfile, Material newMaterial)
        {
            profile = newProfile;
            terrainMaterial = newMaterial;
            Synchronize();
        }

        /// <summary>
        /// Executes the Set Terrains operation.
        /// </summary>
        public void SetTerrains(IReadOnlyList<Terrain> sourceTerrains)
        {
            collectTerrainsFromChildren = false;
            terrains.Clear();
            if (sourceTerrains != null)
            {
                for (int i = 0; i < sourceTerrains.Count; i++)
                {
                    Terrain terrain = sourceTerrains[i];
                    if (terrain != null && !terrains.Contains(terrain))
                    {
                        terrains.Add(terrain);
                    }
                }
            }
            Synchronize();
        }

        private void OnBeginContextRendering(
            ScriptableRenderContext context,
            List<Camera> cameras)
        {
            ApplyRenderBindings();
        }

        private void ApplyRenderBindings()
        {
            if (!renderBindingReady ||
                terrainMaterial == null ||
                profile == null ||
                terrainPropertyBlock == null)
            {
                return;
            }

            for (int i = 0; i < terrains.Count; i++)
            {
                TerrainSurfaceMaterialBinder.BindTerrain(
                    terrains[i],
                    terrainMaterial,
                    activeLayerCount,
                    profile.HeightBlendEnabled,
                    profile,
                    terrainPropertyBlock);
            }
        }

        /// <summary>
        /// Executes the Get Maximum Layer Count operation.
        /// </summary>
        public int GetMaximumLayerCount()
        {
            int maximum = 0;
            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain != null && terrain.terrainData != null)
                {
                    maximum = Mathf.Max(maximum, terrain.terrainData.terrainLayers.Length);
                }
            }

            return maximum;
        }

        /// <summary>
        /// Executes the Collect Terrains From Children operation.
        /// </summary>
        public void CollectTerrainsFromChildren()
        {
            terrains.Clear();
            GetComponentsInChildren(includeInactive: true, terrains);
        }

        private bool TryValidateLayerOrder(out string error)
        {
            IReadOnlyList<TerrainSurfaceLayerSettings> profileLayers = profile.Layers;
            int requiredCount = GetMaximumLayerCount();
            if (profileLayers.Count < requiredCount)
            {
                error = $"The profile contains {profileLayers.Count} layers but the terrain group requires {requiredCount}.";
                return false;
            }

            for (int terrainIndex = 0; terrainIndex < terrains.Count; terrainIndex++)
            {
                Terrain terrain = terrains[terrainIndex];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                TerrainLayer[] terrainLayers = terrain.terrainData.terrainLayers;
                if (terrainLayers.Length != requiredCount)
                {
                    error =
                        $"Terrain '{terrain.name}' has {terrainLayers.Length} layers but the group requires exactly " +
                        $"{requiredCount}. All tiles must share the same ordered layer set.";
                    return false;
                }

                for (int layerIndex = 0; layerIndex < terrainLayers.Length; layerIndex++)
                {
                    if (profileLayers[layerIndex].TerrainLayer == terrainLayers[layerIndex])
                    {
                        continue;
                    }

                    string expected = profileLayers[layerIndex].TerrainLayer != null
                        ? profileLayers[layerIndex].TerrainLayer.name
                        : "<missing>";
                    string actual = terrainLayers[layerIndex] != null ? terrainLayers[layerIndex].name : "<missing>";
                    error =
                        $"Layer order mismatch on '{terrain.name}' at index {layerIndex}: " +
                        $"profile='{expected}', terrain='{actual}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void ApplyFallbackMaterial()
        {
            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain != null)
                {
                    // A null custom material makes Terrain use URP's pipeline default material.
                    terrain.materialTemplate = overLimitFallbackMaterial;
                }
            }
        }

        private void RemoveMissingTerrains()
        {
            for (int i = terrains.Count - 1; i >= 0; i--)
            {
                if (terrains[i] == null)
                {
                    terrains.RemoveAt(i);
                }
            }
        }
    }
}
