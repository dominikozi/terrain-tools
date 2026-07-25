using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dominikozi.TerrainTools
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Terrain Tools/Terrain Surface Mesh Blend")]
    /// <summary>
    /// Represents the Terrain Surface Mesh Blend runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceMeshBlend : MonoBehaviour
    {
        [SerializeField] private TerrainSurfaceGroup terrainGroup;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material blendMaterial;

        [Header("Intersection Blend")]
        [SerializeField] private bool blendEnabled = true;
        [SerializeField, Min(0.001f)] private float blendDistance = 0.5f;
        [SerializeField] private float terrainHeightOffset;
        [SerializeField, Range(0.1f, 8f)] private float blendFalloff = 1f;
        [SerializeField, Range(0f, 1f)] private float terrainNormalBlend = 1f;
        [SerializeField] private bool useVertexAlpha = true;

        [Header("Blend Noise")]
        [SerializeField] private Texture2D blendNoise;
        [SerializeField, Min(0.0001f)] private float noiseWorldScale = 0.1f;
        [SerializeField, Range(0f, 0.95f)] private float noiseStrength = 0.35f;

        [SerializeField, HideInInspector] private string validationMessage;

        private readonly List<Terrain> overlappingTerrains = new();
        private readonly Vector4[] tileOriginSize = new Vector4[TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles];
        private readonly Vector4[] tileHeightData = new Vector4[TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles];
        private readonly Vector4[] tileControlTexelSize = new Vector4[TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles];
        private MaterialPropertyBlock propertyBlock;
        private Vector3 lastBoundsCenter = new Vector3(float.PositiveInfinity, 0f, 0f);
        private int boundLayerCount;
        private int boundTileCount;
        private bool renderBindingReady;

        /// <summary>
        /// Gets the configured Terrain Group value.
        /// </summary>
        public TerrainSurfaceGroup TerrainGroup => terrainGroup;
        /// <summary>
        /// Gets the configured Validation Message value.
        /// </summary>
        public string ValidationMessage => validationMessage;

        private void OnEnable()
        {
            propertyBlock ??= new MaterialPropertyBlock();
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
            blendDistance = Mathf.Max(0.001f, blendDistance);
            blendFalloff = Mathf.Clamp(blendFalloff, 0.1f, 8f);
            terrainNormalBlend = Mathf.Clamp01(terrainNormalBlend);
            noiseWorldScale = Mathf.Max(0.0001f, noiseWorldScale);
            noiseStrength = Mathf.Clamp(noiseStrength, 0f, 0.95f);
            Synchronize();
        }

        private void LateUpdate()
        {
            if (targetRenderer == null)
            {
                return;
            }

            Vector3 center = targetRenderer.bounds.center;
            if ((center - lastBoundsCenter).sqrMagnitude > 0.0001f)
            {
                Synchronize();
            }
        }

        [ContextMenu("Synchronize Terrain Mesh Blend")]
        /// <summary>
        /// Executes the Synchronize operation.
        /// </summary>
        public void Synchronize()
        {
            renderBindingReady = false;
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }
            if (targetRenderer == null)
            {
                validationMessage = "A Renderer is required.";
                return;
            }
            if (terrainGroup == null || terrainGroup.Profile == null)
            {
                validationMessage = "Assign a TerrainSurfaceGroup with a profile.";
                return;
            }
            if (blendMaterial == null)
            {
                validationMessage = "Assign a Terrain Surface Mesh Blend material.";
                return;
            }
            if (!terrainGroup.Profile.HasGeneratedArrays)
            {
                validationMessage = "Build the Terrain Surface texture arrays first.";
                return;
            }

            terrainGroup.Synchronize();
            int layerCount = terrainGroup.GetMaximumLayerCount();
            if (layerCount <= 0 || layerCount > TerrainSurfaceProfile.MaximumShaderLayerCapacity)
            {
                validationMessage = $"Terrain layer count must be between 1 and {TerrainSurfaceProfile.MaximumShaderLayerCapacity}.";
                return;
            }

            CollectOverlappingTerrains(targetRenderer.bounds);
            if (overlappingTerrains.Count == 0)
            {
                validationMessage = "The renderer bounds do not overlap any terrain in the assigned group.";
                return;
            }

            boundLayerCount = layerCount;
            boundTileCount = Mathf.Min(
                overlappingTerrains.Count,
                TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles);
            renderBindingReady = true;
            ApplyRenderBindings();
            lastBoundsCenter = targetRenderer.bounds.center;

            validationMessage = overlappingTerrains.Count > TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles
                ? $"The renderer overlaps {overlappingTerrains.Count} terrain tiles; only the nearest four are bound. Split the mesh for seamless blending."
                : $"Ready: automatically bound {boundTileCount} overlapping terrain tile(s).";
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
                targetRenderer == null ||
                blendMaterial == null ||
                terrainGroup == null ||
                terrainGroup.Profile == null ||
                propertyBlock == null)
            {
                return;
            }

            TerrainSurfaceProfile profile = terrainGroup.Profile;
            targetRenderer.sharedMaterial = blendMaterial;

            targetRenderer.GetPropertyBlock(propertyBlock);
            TerrainSurfaceMaterialBinder.BindProfileProperties(propertyBlock, profile);
            propertyBlock.SetFloat(TerrainSurfaceShaderIds.ActiveLayerCount, boundLayerCount);
            propertyBlock.SetFloat(
                TerrainSurfaceShaderIds.HeightBlend,
                profile.HeightBlendEnabled ? 1f : 0f);
            for (int tileIndex = 0; tileIndex < TerrainSurfaceMeshBlendShaderIds.MaximumBoundTiles; tileIndex++)
            {
                Terrain terrain = tileIndex < boundTileCount ? overlappingTerrains[tileIndex] : null;
                BindTile(propertyBlock, tileIndex, terrain);
            }

            propertyBlock.SetFloat(TerrainSurfaceMeshBlendShaderIds.TileCount, blendEnabled ? boundTileCount : 0);
            propertyBlock.SetVectorArray(TerrainSurfaceMeshBlendShaderIds.TileOriginSize, tileOriginSize);
            propertyBlock.SetVectorArray(TerrainSurfaceMeshBlendShaderIds.TileHeightData, tileHeightData);
            propertyBlock.SetVectorArray(TerrainSurfaceMeshBlendShaderIds.TileControlTexelSize, tileControlTexelSize);
            propertyBlock.SetTexture(
                TerrainSurfaceMeshBlendShaderIds.BlendNoise,
                blendNoise != null ? blendNoise : Texture2D.grayTexture);
            propertyBlock.SetVector(
                TerrainSurfaceMeshBlendShaderIds.BlendParameters,
                new Vector4(blendDistance, terrainHeightOffset, noiseWorldScale, noiseStrength));
            propertyBlock.SetVector(
                TerrainSurfaceMeshBlendShaderIds.BlendExtra,
                new Vector4(useVertexAlpha ? 1f : 0f, blendFalloff, terrainNormalBlend, 0f));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void CollectOverlappingTerrains(Bounds rendererBounds)
        {
            overlappingTerrains.Clear();
            Vector3 rendererCenter = rendererBounds.center;
            for (int i = 0; i < terrainGroup.Terrains.Count; i++)
            {
                Terrain terrain = terrainGroup.Terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                bool overlapsX = rendererBounds.max.x + blendDistance >= origin.x &&
                                 rendererBounds.min.x - blendDistance <= origin.x + size.x;
                bool overlapsZ = rendererBounds.max.z + blendDistance >= origin.z &&
                                 rendererBounds.min.z - blendDistance <= origin.z + size.z;
                if (overlapsX && overlapsZ)
                {
                    overlappingTerrains.Add(terrain);
                }
            }

            overlappingTerrains.Sort((a, b) =>
            {
                Vector3 aCenter = a.transform.position + a.terrainData.size * 0.5f;
                Vector3 bCenter = b.transform.position + b.terrainData.size * 0.5f;
                return (aCenter - rendererCenter).sqrMagnitude.CompareTo((bCenter - rendererCenter).sqrMagnitude);
            });
        }

        private void BindTile(MaterialPropertyBlock block, int tileIndex, Terrain terrain)
        {
            if (terrain == null)
            {
                tileOriginSize[tileIndex] = Vector4.zero;
                tileHeightData[tileIndex] = Vector4.zero;
                tileControlTexelSize[tileIndex] = Vector4.one;
                block.SetTexture(TerrainSurfaceMeshBlendShaderIds.Heights[tileIndex], Texture2D.blackTexture);
                for (int controlIndex = 0; controlIndex < TerrainSurfaceMeshBlendShaderIds.ControlsPerTile; controlIndex++)
                {
                    block.SetTexture(
                        TerrainSurfaceMeshBlendShaderIds.Controls[tileIndex, controlIndex],
                        Texture2D.blackTexture);
                }
                return;
            }

            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            Texture heightmap = data.heightmapTexture;
            tileOriginSize[tileIndex] = new Vector4(origin.x, origin.z, size.x, size.z);
            tileHeightData[tileIndex] = new Vector4(
                origin.y,
                size.y,
                Mathf.Max(1, heightmap.width),
                Mathf.Max(1, heightmap.height));
            tileControlTexelSize[tileIndex] = new Vector4(
                1f / Mathf.Max(1, data.alphamapWidth),
                1f / Mathf.Max(1, data.alphamapHeight),
                data.alphamapWidth,
                data.alphamapHeight);
            block.SetTexture(TerrainSurfaceMeshBlendShaderIds.Heights[tileIndex], heightmap);

            int controlCount = Mathf.Min(TerrainSurfaceMeshBlendShaderIds.ControlsPerTile, data.alphamapTextureCount);
            for (int controlIndex = 0; controlIndex < TerrainSurfaceMeshBlendShaderIds.ControlsPerTile; controlIndex++)
            {
                block.SetTexture(
                    TerrainSurfaceMeshBlendShaderIds.Controls[tileIndex, controlIndex],
                    controlIndex < controlCount ? data.GetAlphamapTexture(controlIndex) : Texture2D.blackTexture);
            }
        }
    }
}
