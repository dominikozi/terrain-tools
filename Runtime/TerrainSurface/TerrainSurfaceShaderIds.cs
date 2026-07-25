using UnityEngine;

namespace Dominikozi.TerrainTools
{
    internal static class TerrainSurfaceShaderIds
    {
        internal static readonly int AlbedoHeightArray = Shader.PropertyToID("_TS_AlbedoHeightArray");
        internal static readonly int NormalSurfaceArray = Shader.PropertyToID("_TS_NormalSurfaceArray");
        internal static readonly int MetallicArray = Shader.PropertyToID("_TS_MetallicArray");
        internal static readonly int ActiveLayerCount = Shader.PropertyToID("_TS_ActiveLayerCount");
        internal static readonly int BlendQuality = Shader.PropertyToID("_TS_BlendQuality");
        internal static readonly int HeightBlend = Shader.PropertyToID("_TS_HeightBlendEnabled");
        internal static readonly int HeightParameters = Shader.PropertyToID("_TS_HeightParameters");
        internal static readonly int TerrainOriginSize = Shader.PropertyToID("_TS_TerrainOriginSize");
        internal static readonly int TerrainSizeY = Shader.PropertyToID("_TS_TerrainSizeY");
        internal static readonly int ControlTexelSize = Shader.PropertyToID("_TS_ControlTexelSize");
        internal static readonly int LayerTiling = Shader.PropertyToID("_TS_LayerTiling");
        internal static readonly int LayerHeightSurface = Shader.PropertyToID("_TS_LayerHeightSurface");
        internal static readonly int LayerSurfaceExtra = Shader.PropertyToID("_TS_LayerSurfaceExtra");
        internal static readonly int LayerAntiTiling = Shader.PropertyToID("_TS_LayerAntiTiling");
        internal static readonly int LayerTriplanar = Shader.PropertyToID("_TS_LayerTriplanar");

        internal static readonly int AntiTilingFlags = Shader.PropertyToID("_TS_AntiTilingFlags");
        internal static readonly int DetailNoise = Shader.PropertyToID("_TS_DetailNoise");
        internal static readonly int DetailNoiseParameters = Shader.PropertyToID("_TS_DetailNoiseParameters");
        internal static readonly int DetailNoiseFade = Shader.PropertyToID("_TS_DetailNoiseFade");
        internal static readonly int MacroNoise = Shader.PropertyToID("_TS_MacroNoise");
        internal static readonly int MacroNoiseParameters = Shader.PropertyToID("_TS_MacroNoiseParameters");
        internal static readonly int MacroNoiseFade = Shader.PropertyToID("_TS_MacroNoiseFade");
        internal static readonly int NormalNoise = Shader.PropertyToID("_TS_NormalNoise");
        internal static readonly int NormalNoiseParameters = Shader.PropertyToID("_TS_NormalNoiseParameters");
        internal static readonly int NormalNoiseFade = Shader.PropertyToID("_TS_NormalNoiseFade");
        internal static readonly int DistanceResampleParameters = Shader.PropertyToID("_TS_DistanceResampleParameters");
        internal static readonly int DistanceResampleFade = Shader.PropertyToID("_TS_DistanceResampleFade");

        internal static readonly int StochasticParameters = Shader.PropertyToID("_TS_StochasticParameters");
        internal static readonly int StochasticExtra = Shader.PropertyToID("_TS_StochasticExtra");

        internal static readonly int GlobalFlags = Shader.PropertyToID("_TS_GlobalFlags");
        internal static readonly int GlobalTint = Shader.PropertyToID("_TS_GlobalTint");
        internal static readonly int GlobalNormal = Shader.PropertyToID("_TS_GlobalNormal");
        internal static readonly int GlobalMapping = Shader.PropertyToID("_TS_GlobalMapping");
        internal static readonly int GlobalParameters = Shader.PropertyToID("_TS_GlobalParameters");
        internal static readonly int GlobalFade = Shader.PropertyToID("_TS_GlobalFade");
        internal static readonly int GlobalFadeOpacity = Shader.PropertyToID("_TS_GlobalFadeOpacity");
        internal static readonly int GlobalReplacement = Shader.PropertyToID("_TS_GlobalReplacement");

        internal static readonly int[] Controls =
        {
            Shader.PropertyToID("_TS_Control0"),
            Shader.PropertyToID("_TS_Control1"),
            Shader.PropertyToID("_TS_Control2"),
            Shader.PropertyToID("_TS_Control3"),
            Shader.PropertyToID("_TS_Control4")
        };
    }
}
