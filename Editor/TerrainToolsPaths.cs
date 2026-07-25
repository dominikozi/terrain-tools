using System.IO;
using UnityEditor;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainToolsPaths
    {
        internal const string PackageId = "com.dominikozi.terrain-tools";
        internal const string PackageRoot = "Packages/" + PackageId;
        internal const string GeneratedRoot = "Assets/Generated/TerrainTools";
        internal const string TerrainSurfaceGeneratedRoot = GeneratedRoot + "/TerrainSurface";

        internal static string PackageAsset(string relativePath)
        {
            return $"{PackageRoot}/{relativePath.TrimStart('/')}";
        }

        internal static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string normalized = path.Replace('\\', '/').TrimEnd('/');
            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parent))
            {
                return;
            }

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }
    }
}
