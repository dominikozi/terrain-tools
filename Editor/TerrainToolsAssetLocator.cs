using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainToolsAssetLocator
    {
        internal const string TerrainShaderName = "Terrain Tools/Terrain Surface Lit";
        internal const string MeshBlendShaderName = "Terrain Tools/Terrain Surface Mesh Blend";
        internal const string PackingShaderName = "Hidden/Terrain Tools/Terrain Surface Array Packing";

        internal static readonly string TerrainShaderPath =
            TerrainToolsPaths.PackageAsset("Runtime/Shaders/TerrainSurfaceLit.shader");

        internal static readonly string MeshBlendShaderPath =
            TerrainToolsPaths.PackageAsset("Runtime/Shaders/TerrainSurfaceMeshBlend.shader");

        internal static readonly string PackingShaderPath =
            TerrainToolsPaths.PackageAsset("Runtime/Shaders/Hidden/TerrainSurfaceArrayPacking.shader");

        private static readonly string DetailNoisePath =
            TerrainToolsPaths.PackageAsset("Runtime/Textures/AntiTiling/TS_DetailNoise_ImageGen.png");

        private static readonly string MacroNoisePath =
            TerrainToolsPaths.PackageAsset("Runtime/Textures/AntiTiling/TS_MacroNoise_ImageGen.png");

        private static readonly string NormalNoisePath =
            TerrainToolsPaths.PackageAsset("Runtime/Textures/AntiTiling/TS_NormalNoise_ImageGen.png");

        internal static Shader FindTerrainShader()
        {
            return Shader.Find(TerrainShaderName);
        }

        internal static Shader FindMeshBlendShader()
        {
            return Shader.Find(MeshBlendShaderName);
        }

        internal static Texture2D LoadDetailNoise()
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DetailNoisePath);
        }

        internal static Texture2D LoadMacroNoise()
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MacroNoisePath);
        }

        internal static Texture2D LoadNormalNoise()
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NormalNoisePath);
        }

        internal static void AssignDefaultProfileAssets(TerrainSurfaceProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            bool changed =
                profile.AntiTiling.DetailNoise == null ||
                profile.AntiTiling.MacroNoise == null ||
                profile.AntiTiling.NormalNoise == null;
            if (!changed)
            {
                return;
            }

            Texture2D detailNoise = LoadDetailNoise();
            Texture2D macroNoise = LoadMacroNoise();
            Texture2D normalNoise = LoadNormalNoise();
            if (detailNoise == null || macroNoise == null || normalNoise == null)
            {
                return;
            }

            profile.AssignDefaultNoiseTextures(
                detailNoise,
                macroNoise,
                normalNoise);
            EditorUtility.SetDirty(profile);
        }
    }
}
