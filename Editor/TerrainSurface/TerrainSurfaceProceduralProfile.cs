using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools
{
    public enum TerrainSurfaceMaskChannel
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3
    }

    [Serializable]
    public sealed class TerrainSurfaceProceduralRule
    {
        [SerializeField] private string label = "Terrain Rule";
        [SerializeField] private bool enabled = true;
        [SerializeField] private TerrainLayer targetLayer;
        [SerializeField] private int priority;
        [SerializeField, Range(0f, 1f)] private float strength = 1f;
        [SerializeField] private Color previewColor = Color.white;

        [Header("World Height")]
        [SerializeField] private bool heightEnabled;
        [SerializeField] private Vector2 heightRange = new Vector2(0f, 1000f);
        [SerializeField, Min(0f)] private float heightFalloff = 10f;

        [Header("Slope")]
        [SerializeField] private bool slopeEnabled;
        [SerializeField] private Vector2 slopeRange = new Vector2(0f, 90f);
        [SerializeField, Range(0f, 45f)] private float slopeFalloff = 5f;

        [Header("Cavity / Ridge")]
        [SerializeField] private bool cavityEnabled;
        [SerializeField] private Vector2 cavityRange = new Vector2(-1f, 1f);
        [SerializeField, Range(0.001f, 1f)] private float cavityFalloff = 0.1f;
        [SerializeField, Min(0.1f)] private float cavityRadius = 4f;
        [SerializeField, Min(0.0001f)] private float cavityScale = 0.25f;

        [Header("World-Space Noise")]
        [SerializeField] private bool noiseEnabled;
        [SerializeField, Min(0.000001f)] private float noiseWorldScale = 0.01f;
        [SerializeField, Range(1, 6)] private int noiseOctaves = 3;
        [SerializeField, Range(0.1f, 0.9f)] private float noisePersistence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float noiseThreshold = 0.5f;
        [SerializeField, Range(0.001f, 0.5f)] private float noiseTransition = 0.1f;
        [SerializeField] private int noiseSeed;

        [Header("World Region Mask")]
        [SerializeField] private bool regionMaskEnabled;
        [SerializeField] private Texture2D regionMask;
        [SerializeField] private TerrainSurfaceMaskChannel regionMaskChannel;
        [SerializeField] private bool invertRegionMask;
        [SerializeField] private Vector2 regionWorldSize = new Vector2(2048f, 2048f);
        [SerializeField] private Vector2 regionWorldOffset;

        public string Label => string.IsNullOrWhiteSpace(label) ? "Terrain Rule" : label;
        public bool Enabled => enabled && targetLayer != null && strength > 0f;
        public TerrainLayer TargetLayer => targetLayer;
        public int Priority => priority;
        public float Strength => strength;
        public Color PreviewColor => previewColor;
        public bool HeightEnabled => heightEnabled;
        public Vector2 HeightRange => Ordered(heightRange);
        public float HeightFalloff => heightFalloff;
        public bool SlopeEnabled => slopeEnabled;
        public Vector2 SlopeRange => Ordered(slopeRange);
        public float SlopeFalloff => slopeFalloff;
        public bool CavityEnabled => cavityEnabled;
        public Vector2 CavityRange => Ordered(cavityRange);
        public float CavityFalloff => cavityFalloff;
        public float CavityRadius => cavityRadius;
        public float CavityScale => cavityScale;
        public bool NoiseEnabled => noiseEnabled;
        public float NoiseWorldScale => noiseWorldScale;
        public int NoiseOctaves => noiseOctaves;
        public float NoisePersistence => noisePersistence;
        public float NoiseThreshold => noiseThreshold;
        public float NoiseTransition => noiseTransition;
        public int NoiseSeed => noiseSeed;
        public bool RegionMaskEnabled => regionMaskEnabled && regionMask != null;
        public Texture2D RegionMask => regionMask;
        public TerrainSurfaceMaskChannel RegionMaskChannel => regionMaskChannel;
        public bool InvertRegionMask => invertRegionMask;
        public Vector2 RegionWorldSize => new Vector2(
            Mathf.Max(0.01f, regionWorldSize.x),
            Mathf.Max(0.01f, regionWorldSize.y));
        public Vector2 RegionWorldOffset => regionWorldOffset;

        internal void ClampValues()
        {
            strength = Mathf.Clamp01(strength);
            heightFalloff = Mathf.Max(0f, heightFalloff);
            slopeRange.x = Mathf.Clamp(slopeRange.x, 0f, 90f);
            slopeRange.y = Mathf.Clamp(slopeRange.y, 0f, 90f);
            slopeFalloff = Mathf.Clamp(slopeFalloff, 0f, 45f);
            cavityRange.x = Mathf.Clamp(cavityRange.x, -1f, 1f);
            cavityRange.y = Mathf.Clamp(cavityRange.y, -1f, 1f);
            cavityFalloff = Mathf.Clamp(cavityFalloff, 0.001f, 1f);
            cavityRadius = Mathf.Max(0.1f, cavityRadius);
            cavityScale = Mathf.Max(0.0001f, cavityScale);
            noiseWorldScale = Mathf.Max(0.000001f, noiseWorldScale);
            noiseOctaves = Mathf.Clamp(noiseOctaves, 1, 6);
            noisePersistence = Mathf.Clamp(noisePersistence, 0.1f, 0.9f);
            noiseThreshold = Mathf.Clamp01(noiseThreshold);
            noiseTransition = Mathf.Clamp(noiseTransition, 0.001f, 0.5f);
            regionWorldSize.x = Mathf.Max(0.01f, regionWorldSize.x);
            regionWorldSize.y = Mathf.Max(0.01f, regionWorldSize.y);
        }

        private static Vector2 Ordered(Vector2 value)
        {
            return value.x <= value.y ? value : new Vector2(value.y, value.x);
        }
    }

    [CreateAssetMenu(
        fileName = "TerrainProceduralProfile",
        menuName = "Terrain Tools/Terrain Surface/Procedural Profile")]
    public sealed class TerrainSurfaceProceduralProfile : ScriptableObject
    {
        [SerializeField] private TerrainLayer fallbackLayer;
        [SerializeField] private Color fallbackPreviewColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private List<TerrainSurfaceProceduralRule> rules = new();

        public TerrainLayer FallbackLayer => fallbackLayer;
        public Color FallbackPreviewColor => fallbackPreviewColor;
        public IReadOnlyList<TerrainSurfaceProceduralRule> Rules => rules;

        private void OnValidate()
        {
            rules ??= new List<TerrainSurfaceProceduralRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i]?.ClampValues();
            }
        }
    }

    [Serializable]
    public sealed class TerrainSurfaceAlphamapBackupEntry
    {
        [SerializeField] private TerrainData terrainData;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int layerCount;
        [SerializeField] private byte[] compressedWeights;

        public TerrainData TerrainData => terrainData;
        public int Width => width;
        public int Height => height;
        public int LayerCount => layerCount;
        public byte[] CompressedWeights => compressedWeights;

        public TerrainSurfaceAlphamapBackupEntry(
            TerrainData source,
            int sourceWidth,
            int sourceHeight,
            int sourceLayerCount,
            byte[] sourceCompressedWeights)
        {
            terrainData = source;
            width = sourceWidth;
            height = sourceHeight;
            layerCount = sourceLayerCount;
            compressedWeights = sourceCompressedWeights;
        }
    }

    public sealed class TerrainSurfaceAlphamapBackup : ScriptableObject
    {
        [SerializeField] private string createdUtc;
        [SerializeField] private List<TerrainSurfaceAlphamapBackupEntry> entries = new();

        public string CreatedUtc => createdUtc;
        public IReadOnlyList<TerrainSurfaceAlphamapBackupEntry> Entries => entries;

        public void Initialize(List<TerrainSurfaceAlphamapBackupEntry> sourceEntries)
        {
            createdUtc = DateTime.UtcNow.ToString("O");
            entries = sourceEntries ?? new List<TerrainSurfaceAlphamapBackupEntry>();
        }
    }
}
