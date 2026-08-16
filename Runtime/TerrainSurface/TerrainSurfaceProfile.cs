using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools
{
    /// <summary>
    /// Defines the supported Terrain Surface Blend Quality values.
    /// </summary>
    public enum TerrainSurfaceBlendQuality
    {
        Top2 = 2,
        Top3 = 3,
        Top4 = 4
    }

    /// <summary>
    /// Defines the supported Terrain Surface Global Blend Mode values.
    /// </summary>
    public enum TerrainSurfaceGlobalBlendMode
    {
        Multiply = 0,
        Overlay = 1,
        CrossFade = 2
    }

    [Serializable]
    /// <summary>
    /// Represents the Terrain Surface Layer Settings runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceLayerSettings
    {
        [SerializeField] private TerrainLayer terrainLayer;

        [Header("Height Blend")]
        [SerializeField, Range(-1f, 1f)] private float heightOffset;
        [SerializeField, Min(0f)] private float heightContrast = 1f;

        [Header("Surface")]
        [SerializeField] private bool tintEnabled;
        [SerializeField, ColorUsage(false, false)] private Color tint = Color.white;
        [SerializeField, Min(0f)] private float normalStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float metallicMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float smoothnessMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float ambientOcclusionStrength = 1f;

        [Header("Anti-Tiling")]
        [SerializeField, Range(0f, 2f)] private float detailNoiseStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float macroNoiseStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float normalNoiseStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float distanceResampleStrength = 1f;

        [Header("Stochastic Sampling")]
        [Tooltip("Allows this layer to use stochastic sampling when the profile-wide module is enabled.")]
        [SerializeField] private bool stochasticSampling = true;

        [Header("Triplanar")]
        [SerializeField] private bool triplanar;
        [SerializeField, Min(0.01f)] private float triplanarScale = 1f;
        [Tooltip("Controls projection blending and moves the hard top/side boundary. Higher values keep top projection on steeper heightfield transition triangles.")]
        [SerializeField, Range(0.25f, 32f)] private float triplanarSharpness = 4f;
        [SerializeField, Range(0.0001f, 1f)] private float triplanarHeightTransition = 0.15f;

        /// <summary>
        /// Gets the configured Terrain Layer value.
        /// </summary>
        public TerrainLayer TerrainLayer => terrainLayer;
        /// <summary>
        /// Gets the configured Height Offset value.
        /// </summary>
        public float HeightOffset => heightOffset;
        /// <summary>
        /// Gets the configured Height Contrast value.
        /// </summary>
        public float HeightContrast => heightContrast;
        /// <summary>
        /// Gets a value indicating whether the per-layer tint is enabled.
        /// </summary>
        public bool TintEnabled => tintEnabled;
        /// <summary>
        /// Gets the configured per-layer tint value.
        /// </summary>
        public Color Tint => tint;
        /// <summary>
        /// Gets the configured Normal Strength value.
        /// </summary>
        public float NormalStrength => normalStrength;
        /// <summary>
        /// Gets the configured Metallic Multiplier value.
        /// </summary>
        public float MetallicMultiplier => metallicMultiplier;
        /// <summary>
        /// Gets the configured Smoothness Multiplier value.
        /// </summary>
        public float SmoothnessMultiplier => smoothnessMultiplier;
        /// <summary>
        /// Gets the configured Ambient Occlusion Strength value.
        /// </summary>
        public float AmbientOcclusionStrength => ambientOcclusionStrength;
        /// <summary>
        /// Gets the configured Detail Noise Strength value.
        /// </summary>
        public float DetailNoiseStrength => detailNoiseStrength;
        /// <summary>
        /// Gets the configured Macro Noise Strength value.
        /// </summary>
        public float MacroNoiseStrength => macroNoiseStrength;
        /// <summary>
        /// Gets the configured Normal Noise Strength value.
        /// </summary>
        public float NormalNoiseStrength => normalNoiseStrength;
        /// <summary>
        /// Gets the configured Distance Resample Strength value.
        /// </summary>
        public float DistanceResampleStrength => distanceResampleStrength;
        /// <summary>
        /// Gets the configured Stochastic Sampling value.
        /// </summary>
        public bool StochasticSampling => stochasticSampling;
        /// <summary>
        /// Gets the configured Triplanar value.
        /// </summary>
        public bool Triplanar => triplanar;
        /// <summary>
        /// Gets the configured Triplanar Scale value.
        /// </summary>
        public float TriplanarScale => triplanarScale;
        /// <summary>
        /// Gets the projection blend sharpness and top-to-side boundary bias.
        /// </summary>
        public float TriplanarSharpness => triplanarSharpness;
        /// <summary>
        /// Gets the configured Triplanar Height Transition value.
        /// </summary>
        public float TriplanarHeightTransition => triplanarHeightTransition;

        /// <summary>
        /// Initializes a new Terrain Surface Layer Settings instance.
        /// </summary>
        public TerrainSurfaceLayerSettings(TerrainLayer source)
        {
            terrainLayer = source;
            if (source == null)
            {
                return;
            }

            normalStrength = Mathf.Max(0f, source.normalScale);
            smoothnessMultiplier = 1f;
        }

        internal void SetTerrainLayer(TerrainLayer source)
        {
            terrainLayer = source;
        }

        internal void ClampValues()
        {
            heightOffset = Mathf.Clamp(heightOffset, -1f, 1f);
            heightContrast = Mathf.Max(0f, heightContrast);
            tint.r = Mathf.Clamp01(tint.r);
            tint.g = Mathf.Clamp01(tint.g);
            tint.b = Mathf.Clamp01(tint.b);
            tint.a = 1f;
            normalStrength = Mathf.Max(0f, normalStrength);
            metallicMultiplier = Mathf.Clamp(metallicMultiplier, 0f, 2f);
            smoothnessMultiplier = Mathf.Clamp(smoothnessMultiplier, 0f, 2f);
            ambientOcclusionStrength = Mathf.Clamp(ambientOcclusionStrength, 0f, 2f);
            detailNoiseStrength = Mathf.Clamp(detailNoiseStrength, 0f, 2f);
            macroNoiseStrength = Mathf.Clamp(macroNoiseStrength, 0f, 2f);
            normalNoiseStrength = Mathf.Clamp(normalNoiseStrength, 0f, 2f);
            distanceResampleStrength = Mathf.Clamp(distanceResampleStrength, 0f, 2f);
            triplanarScale = Mathf.Max(0.01f, triplanarScale);
            triplanarSharpness = Mathf.Clamp(triplanarSharpness, 0.25f, 32f);
            triplanarHeightTransition = Mathf.Clamp(triplanarHeightTransition, 0.0001f, 1f);
        }
    }

    [Serializable]
    /// <summary>
    /// Represents the Terrain Surface Anti Tiling Settings runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceAntiTilingSettings
    {
        [SerializeField] private bool enabled;

        [Header("Detail Noise")]
        [SerializeField] private bool detailNoiseEnabled = true;
        [SerializeField] private Texture2D detailNoise;
        [SerializeField, Min(0.0001f)] private float detailWorldScale = 0.5f;
        [SerializeField, Range(0f, 2f)] private float detailStrength = 0.25f;
        [SerializeField, Min(0f)] private float detailFadeStart = 8f;
        [SerializeField, Min(0f)] private float detailFadeEnd = 40f;

        [Header("Macro / Distance Noise")]
        [SerializeField] private bool macroNoiseEnabled = true;
        [SerializeField] private Texture2D macroNoise;
        [SerializeField, Min(0.0001f)] private float macroWorldScale = 0.01f;
        [SerializeField, Range(0f, 2f)] private float macroStrength = 0.2f;
        [SerializeField, Min(0f)] private float macroFadeStart = 20f;
        [SerializeField, Min(0f)] private float macroFadeEnd = 120f;

        [Header("Normal Noise")]
        [SerializeField] private bool normalNoiseEnabled = true;
        [SerializeField] private Texture2D normalNoise;
        [SerializeField, Min(0.0001f)] private float normalNoiseWorldScale = 0.04f;
        [SerializeField, Range(0f, 2f)] private float normalNoiseStrength = 0.35f;
        [SerializeField, Min(0f)] private float normalNoiseFadeStart;
        [SerializeField, Min(0f)] private float normalNoiseFadeEnd = 200f;

        [Header("Distance Resampling")]
        [SerializeField] private bool distanceResamplingEnabled;
        [SerializeField, Min(0.01f)] private float distanceResampleScale = 0.2f;
        [SerializeField, Range(0f, 1f)] private float distanceResampleStrength = 1f;
        [SerializeField, Min(0f)] private float distanceResampleFadeStart = 40f;
        [SerializeField, Min(0f)] private float distanceResampleFadeEnd = 160f;
        [SerializeField] private bool distanceResampleHeightBlend = true;

        /// <summary>
        /// Gets a value indicating whether Enabled.
        /// </summary>
        public bool Enabled => enabled;
        /// <summary>
        /// Gets the configured Detail Noise Enabled value.
        /// </summary>
        public bool DetailNoiseEnabled => enabled && detailNoiseEnabled && detailNoise != null;
        /// <summary>
        /// Gets the configured Detail Noise value.
        /// </summary>
        public Texture2D DetailNoise => detailNoise;
        /// <summary>
        /// Gets the configured Detail World Scale value.
        /// </summary>
        public float DetailWorldScale => detailWorldScale;
        /// <summary>
        /// Gets the configured Detail Strength value.
        /// </summary>
        public float DetailStrength => detailStrength;
        /// <summary>
        /// Gets the configured Detail Fade value.
        /// </summary>
        public Vector2 DetailFade => OrderedRange(detailFadeStart, detailFadeEnd);
        /// <summary>
        /// Gets the configured Macro Noise Enabled value.
        /// </summary>
        public bool MacroNoiseEnabled => enabled && macroNoiseEnabled && macroNoise != null;
        /// <summary>
        /// Gets the configured Macro Noise value.
        /// </summary>
        public Texture2D MacroNoise => macroNoise;
        /// <summary>
        /// Gets the configured Macro World Scale value.
        /// </summary>
        public float MacroWorldScale => macroWorldScale;
        /// <summary>
        /// Gets the configured Macro Strength value.
        /// </summary>
        public float MacroStrength => macroStrength;
        /// <summary>
        /// Gets the configured Macro Fade value.
        /// </summary>
        public Vector2 MacroFade => OrderedRange(macroFadeStart, macroFadeEnd);
        /// <summary>
        /// Gets the configured Normal Noise Enabled value.
        /// </summary>
        public bool NormalNoiseEnabled => enabled && normalNoiseEnabled && normalNoise != null;
        /// <summary>
        /// Gets the configured Normal Noise value.
        /// </summary>
        public Texture2D NormalNoise => normalNoise;
        /// <summary>
        /// Gets the configured Normal Noise World Scale value.
        /// </summary>
        public float NormalNoiseWorldScale => normalNoiseWorldScale;
        /// <summary>
        /// Gets the configured Normal Noise Strength value.
        /// </summary>
        public float NormalNoiseStrength => normalNoiseStrength;
        /// <summary>
        /// Gets the configured Normal Noise Fade value.
        /// </summary>
        public Vector2 NormalNoiseFade => OrderedRange(normalNoiseFadeStart, normalNoiseFadeEnd);
        /// <summary>
        /// Gets the configured Distance Resampling Enabled value.
        /// </summary>
        public bool DistanceResamplingEnabled => enabled && distanceResamplingEnabled;
        /// <summary>
        /// Gets the configured Distance Resample Scale value.
        /// </summary>
        public float DistanceResampleScale => distanceResampleScale;
        /// <summary>
        /// Gets the configured Distance Resample Strength value.
        /// </summary>
        public float DistanceResampleStrength => distanceResampleStrength;
        /// <summary>
        /// Gets the configured Distance Resample Fade value.
        /// </summary>
        public Vector2 DistanceResampleFade => OrderedRange(distanceResampleFadeStart, distanceResampleFadeEnd);
        /// <summary>
        /// Gets the configured Distance Resample Height Blend value.
        /// </summary>
        public bool DistanceResampleHeightBlend => distanceResampleHeightBlend;

        internal void AssignDefaultTextures(
            Texture2D defaultDetailNoise,
            Texture2D defaultMacroNoise,
            Texture2D defaultNormalNoise)
        {
            detailNoise ??= defaultDetailNoise;
            macroNoise ??= defaultMacroNoise;
            normalNoise ??= defaultNormalNoise;
        }

        internal void ClampValues()
        {
            detailWorldScale = Mathf.Max(0.0001f, detailWorldScale);
            detailStrength = Mathf.Clamp(detailStrength, 0f, 2f);
            detailFadeStart = Mathf.Max(0f, detailFadeStart);
            detailFadeEnd = Mathf.Max(0f, detailFadeEnd);
            macroWorldScale = Mathf.Max(0.0001f, macroWorldScale);
            macroStrength = Mathf.Clamp(macroStrength, 0f, 2f);
            macroFadeStart = Mathf.Max(0f, macroFadeStart);
            macroFadeEnd = Mathf.Max(0f, macroFadeEnd);
            normalNoiseWorldScale = Mathf.Max(0.0001f, normalNoiseWorldScale);
            normalNoiseStrength = Mathf.Clamp(normalNoiseStrength, 0f, 2f);
            normalNoiseFadeStart = Mathf.Max(0f, normalNoiseFadeStart);
            normalNoiseFadeEnd = Mathf.Max(0f, normalNoiseFadeEnd);
            distanceResampleScale = Mathf.Max(0.01f, distanceResampleScale);
            distanceResampleStrength = Mathf.Clamp01(distanceResampleStrength);
            distanceResampleFadeStart = Mathf.Max(0f, distanceResampleFadeStart);
            distanceResampleFadeEnd = Mathf.Max(0f, distanceResampleFadeEnd);
        }

        private static Vector2 OrderedRange(float a, float b)
        {
            return a <= b ? new Vector2(a, b) : new Vector2(b, a);
        }
    }

    [Serializable]
    /// <summary>
    /// Represents the Terrain Surface Stochastic Settings runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceStochasticSettings
    {
        [Tooltip("Master switch. When disabled, stochastic sampling does not change terrain rendering.")]
        [SerializeField] private bool enabled;
        [Tooltip("Density of the stochastic triangle grid relative to the TerrainLayer tiling.")]
        [SerializeField, Range(0.25f, 4f)] private float gridScale = 1f;
        [Tooltip("Sharpens or softens the triangle-grid interpolation before height-aware blending.")]
        [SerializeField, Range(0.25f, 8f)] private float blendContrast = 1f;
        [Tooltip("Uses the layer height channel to reduce the soft, blurry look caused by blending three samples.")]
        [SerializeField] private bool heightBlend = true;
        [Tooltip("Width of height-aware transitions between the three stochastic samples.")]
        [SerializeField, Range(0.0001f, 1f)] private float heightTransition = 0.15f;
        [Tooltip("Also rotates samples by random multiples of 90 degrees. Disabled matches MicroSplat's offset-only baseline.")]
        [SerializeField] private bool randomQuarterTurns;
        [Tooltip("Changes the stable random pattern without changing painted splat weights.")]
        [SerializeField] private int seed;

        /// <summary>
        /// Gets a value indicating whether Enabled.
        /// </summary>
        public bool Enabled => enabled;
        /// <summary>
        /// Gets the configured Grid Scale value.
        /// </summary>
        public float GridScale => gridScale;
        /// <summary>
        /// Gets the configured Blend Contrast value.
        /// </summary>
        public float BlendContrast => blendContrast;
        /// <summary>
        /// Gets the configured Height Blend value.
        /// </summary>
        public bool HeightBlend => heightBlend;
        /// <summary>
        /// Gets the configured Height Transition value.
        /// </summary>
        public float HeightTransition => heightTransition;
        /// <summary>
        /// Gets the configured Random Quarter Turns value.
        /// </summary>
        public bool RandomQuarterTurns => randomQuarterTurns;
        /// <summary>
        /// Gets the configured Seed value.
        /// </summary>
        public int Seed => seed;

        internal void ClampValues()
        {
            gridScale = Mathf.Clamp(gridScale, 0.25f, 4f);
            blendContrast = Mathf.Clamp(blendContrast, 0.25f, 8f);
            heightTransition = Mathf.Clamp(heightTransition, 0.0001f, 1f);
        }
    }

    [Serializable]
    /// <summary>
    /// Represents the Terrain Surface Global Texturing Settings runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceGlobalTexturingSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private Texture2D globalTint;
        [SerializeField] private Texture2D globalNormal;
        [SerializeField] private TerrainSurfaceGlobalBlendMode tintBlendMode = TerrainSurfaceGlobalBlendMode.Multiply;
        [SerializeField] private Vector2 worldSize = new Vector2(2048f, 2048f);
        [SerializeField] private Vector2 worldOffset;
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.5f;
        [SerializeField, Range(0f, 2f)] private float normalStrength = 0.5f;
        [SerializeField, Min(0f)] private float fadeStart;
        [SerializeField, Min(0f)] private float fadeEnd = 1000f;
        [SerializeField, Range(0f, 1f)] private float opacityAtFadeStart = 1f;
        [SerializeField, Range(0f, 1f)] private float opacityAtFadeEnd = 1f;
        [SerializeField] private bool replaceSplatInDistance;
        [SerializeField, Min(0f)] private float replacementFadeStart = 500f;
        [SerializeField, Min(0f)] private float replacementFadeEnd = 1200f;
        [SerializeField, Range(0f, 1f)] private float replacementStrength = 1f;

        /// <summary>
        /// Gets a value indicating whether Enabled.
        /// </summary>
        public bool Enabled => enabled && (globalTint != null || globalNormal != null);
        /// <summary>
        /// Gets the configured Global Tint value.
        /// </summary>
        public Texture2D GlobalTint => globalTint;
        /// <summary>
        /// Gets the configured Global Normal value.
        /// </summary>
        public Texture2D GlobalNormal => globalNormal;
        /// <summary>
        /// Gets the configured Tint Blend Mode value.
        /// </summary>
        public TerrainSurfaceGlobalBlendMode TintBlendMode => tintBlendMode;
        /// <summary>
        /// Gets the configured World Size value.
        /// </summary>
        public Vector2 WorldSize => new Vector2(Mathf.Max(0.01f, worldSize.x), Mathf.Max(0.01f, worldSize.y));
        /// <summary>
        /// Gets the configured World Offset value.
        /// </summary>
        public Vector2 WorldOffset => worldOffset;
        /// <summary>
        /// Gets the configured Tint Strength value.
        /// </summary>
        public float TintStrength => tintStrength;
        /// <summary>
        /// Gets the configured Normal Strength value.
        /// </summary>
        public float NormalStrength => normalStrength;
        /// <summary>
        /// Gets the configured Fade value.
        /// </summary>
        public Vector2 Fade => fadeStart <= fadeEnd ? new Vector2(fadeStart, fadeEnd) : new Vector2(fadeEnd, fadeStart);
        /// <summary>
        /// Gets the configured Fade Opacity value.
        /// </summary>
        public Vector2 FadeOpacity => new Vector2(opacityAtFadeStart, opacityAtFadeEnd);
        /// <summary>
        /// Gets the configured Replace Splat In Distance value.
        /// </summary>
        public bool ReplaceSplatInDistance => Enabled && replaceSplatInDistance && globalTint != null;
        /// <summary>
        /// Gets the configured Replacement Fade value.
        /// </summary>
        public Vector2 ReplacementFade => replacementFadeStart <= replacementFadeEnd
            ? new Vector2(replacementFadeStart, replacementFadeEnd)
            : new Vector2(replacementFadeEnd, replacementFadeStart);
        /// <summary>
        /// Gets the configured Replacement Strength value.
        /// </summary>
        public float ReplacementStrength => replacementStrength;

        internal void ClampValues()
        {
            worldSize.x = Mathf.Max(0.01f, worldSize.x);
            worldSize.y = Mathf.Max(0.01f, worldSize.y);
            tintStrength = Mathf.Clamp01(tintStrength);
            normalStrength = Mathf.Clamp(normalStrength, 0f, 2f);
            fadeStart = Mathf.Max(0f, fadeStart);
            fadeEnd = Mathf.Max(0f, fadeEnd);
            opacityAtFadeStart = Mathf.Clamp01(opacityAtFadeStart);
            opacityAtFadeEnd = Mathf.Clamp01(opacityAtFadeEnd);
            replacementFadeStart = Mathf.Max(0f, replacementFadeStart);
            replacementFadeEnd = Mathf.Max(0f, replacementFadeEnd);
            replacementStrength = Mathf.Clamp01(replacementStrength);
        }
    }

    [CreateAssetMenu(fileName = "TerrainSurfaceProfile", menuName = "Terrain Tools/Terrain Surface Profile")]
    /// <summary>
    /// Represents the Terrain Surface Profile runtime component or configuration.
    /// </summary>
    public sealed class TerrainSurfaceProfile : ScriptableObject
    {
        /// <summary>
        /// Provides part of the supported Terrain Tools runtime API.
        /// </summary>
        public const int MinimumShaderLayerCapacity = 12;
        /// <summary>
        /// Provides part of the supported Terrain Tools runtime API.
        /// </summary>
        public const int MaximumShaderLayerCapacity = 20;

        [Header("Blending")]
        [SerializeField] private bool heightBlendEnabled = true;
        [SerializeField] private TerrainSurfaceBlendQuality blendQuality = TerrainSurfaceBlendQuality.Top4;
        [SerializeField, Range(0.0001f, 1f)] private float heightTransition = 0.15f;
        [SerializeField, Range(-1f, 1f)] private float globalHeightOffset;
        [SerializeField, Min(0f)] private float globalHeightContrast = 1f;

        [Header("Texture Arrays")]
        [SerializeField] private int textureResolution = 1024;
        [SerializeField, Range(-3f, 3f)] private float textureMipBias = -1f;
        [SerializeField, HideInInspector] private Texture2DArray albedoHeightArray;
        [SerializeField, HideInInspector] private Texture2DArray normalSurfaceArray;
        [SerializeField, HideInInspector] private Texture2DArray metallicArray;
        [SerializeField] private List<TerrainSurfaceLayerSettings> layers = new();

        [Header("Modules")]
        [SerializeField] private TerrainSurfaceAntiTilingSettings antiTiling = new();
        [SerializeField] private TerrainSurfaceStochasticSettings stochasticSampling = new();
        [SerializeField] private TerrainSurfaceGlobalTexturingSettings globalTexturing = new();

        /// <summary>
        /// Gets the configured Height Blend Enabled value.
        /// </summary>
        public bool HeightBlendEnabled => heightBlendEnabled;
        /// <summary>
        /// Gets the configured Blend Quality value.
        /// </summary>
        public TerrainSurfaceBlendQuality BlendQuality => blendQuality;
        /// <summary>
        /// Gets the configured Height Transition value.
        /// </summary>
        public float HeightTransition => heightTransition;
        /// <summary>
        /// Gets the configured Global Height Offset value.
        /// </summary>
        public float GlobalHeightOffset => globalHeightOffset;
        /// <summary>
        /// Gets the configured Global Height Contrast value.
        /// </summary>
        public float GlobalHeightContrast => globalHeightContrast;
        /// <summary>
        /// Gets the configured Texture Resolution value.
        /// </summary>
        public int TextureResolution => textureResolution;
        /// <summary>
        /// Gets the configured Texture Mip Bias value.
        /// </summary>
        public float TextureMipBias => textureMipBias;
        /// <summary>
        /// Gets the configured Albedo Height Array value.
        /// </summary>
        public Texture2DArray AlbedoHeightArray => albedoHeightArray;
        /// <summary>
        /// Gets the configured Normal Surface Array value.
        /// </summary>
        public Texture2DArray NormalSurfaceArray => normalSurfaceArray;
        /// <summary>
        /// Gets the configured Metallic Array value.
        /// </summary>
        public Texture2DArray MetallicArray => metallicArray;
        /// <summary>
        /// Gets the configured Layers value.
        /// </summary>
        public IReadOnlyList<TerrainSurfaceLayerSettings> Layers => layers;
        /// <summary>
        /// Gets the configured Anti Tiling value.
        /// </summary>
        public TerrainSurfaceAntiTilingSettings AntiTiling => antiTiling;
        /// <summary>
        /// Gets the configured Stochastic Sampling value.
        /// </summary>
        public TerrainSurfaceStochasticSettings StochasticSampling => stochasticSampling;
        /// <summary>
        /// Gets the configured Global Texturing value.
        /// </summary>
        public TerrainSurfaceGlobalTexturingSettings GlobalTexturing => globalTexturing;
        /// <summary>
        /// Gets a value indicating whether Has Generated Arrays.
        /// </summary>
        public bool HasGeneratedArrays =>
            albedoHeightArray != null && normalSurfaceArray != null && metallicArray != null;

        /// <summary>
        /// Executes the Synchronize Layers operation.
        /// </summary>
        public void SynchronizeLayers(IReadOnlyList<TerrainLayer> sourceLayers)
        {
            sourceLayers ??= Array.Empty<TerrainLayer>();
            Dictionary<TerrainLayer, TerrainSurfaceLayerSettings> existing = new();
            for (int i = 0; i < layers.Count; i++)
            {
                TerrainSurfaceLayerSettings settings = layers[i];
                if (settings?.TerrainLayer != null && !existing.ContainsKey(settings.TerrainLayer))
                {
                    existing.Add(settings.TerrainLayer, settings);
                }
            }

            List<TerrainSurfaceLayerSettings> synchronized = new(sourceLayers.Count);
            for (int i = 0; i < sourceLayers.Count; i++)
            {
                TerrainLayer source = sourceLayers[i];
                if (source != null && existing.TryGetValue(source, out TerrainSurfaceLayerSettings settings))
                {
                    settings.SetTerrainLayer(source);
                    synchronized.Add(settings);
                }
                else
                {
                    synchronized.Add(new TerrainSurfaceLayerSettings(source));
                }
            }

            layers = synchronized;
            OnValidate();
        }

        /// <summary>
        /// Executes the Assign Generated Arrays operation.
        /// </summary>
        public void AssignGeneratedArrays(
            Texture2DArray albedoHeight,
            Texture2DArray normalSurface,
            Texture2DArray metallic)
        {
            albedoHeightArray = albedoHeight;
            normalSurfaceArray = normalSurface;
            metallicArray = metallic;
        }

        internal void AssignDefaultNoiseTextures(
            Texture2D detailNoise,
            Texture2D macroNoise,
            Texture2D normalNoise)
        {
            antiTiling ??= new TerrainSurfaceAntiTilingSettings();
            antiTiling.AssignDefaultTextures(detailNoise, macroNoise, normalNoise);
        }

        /// <summary>
        /// Executes the Get Shader Layer Capacity operation.
        /// </summary>
        public int GetShaderLayerCapacity(int actualLayerCount)
        {
            if (actualLayerCount <= 0)
            {
                return 0;
            }

            if (actualLayerCount > MaximumShaderLayerCapacity)
            {
                return 0;
            }

            if (actualLayerCount <= MinimumShaderLayerCapacity)
            {
                return MinimumShaderLayerCapacity;
            }

            return actualLayerCount <= 16 ? 16 : MaximumShaderLayerCapacity;
        }

        private void OnValidate()
        {
            heightTransition = Mathf.Clamp(heightTransition, 0.0001f, 1f);
            globalHeightOffset = Mathf.Clamp(globalHeightOffset, -1f, 1f);
            globalHeightContrast = Mathf.Max(0f, globalHeightContrast);
            textureResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(textureResolution), 128, 4096);
            textureMipBias = Mathf.Clamp(textureMipBias, -3f, 3f);
            layers ??= new List<TerrainSurfaceLayerSettings>();
            antiTiling ??= new TerrainSurfaceAntiTilingSettings();
            stochasticSampling ??= new TerrainSurfaceStochasticSettings();
            globalTexturing ??= new TerrainSurfaceGlobalTexturingSettings();

            for (int i = 0; i < layers.Count; i++)
            {
                layers[i]?.ClampValues();
            }

            antiTiling.ClampValues();
            stochasticSampling.ClampValues();
            globalTexturing.ClampValues();
        }
    }
}
