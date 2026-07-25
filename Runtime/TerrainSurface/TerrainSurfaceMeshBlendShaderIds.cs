using UnityEngine;

namespace Dominikozi.TerrainTools
{
    internal static class TerrainSurfaceMeshBlendShaderIds
    {
        internal const int MaximumBoundTiles = 4;
        internal const int ControlsPerTile = 5;

        internal static readonly int TileCount = Shader.PropertyToID("_TS_BlendTileCount");
        internal static readonly int TileOriginSize = Shader.PropertyToID("_TS_BlendTileOriginSize");
        internal static readonly int TileHeightData = Shader.PropertyToID("_TS_BlendTileHeightData");
        internal static readonly int TileControlTexelSize = Shader.PropertyToID("_TS_BlendTileControlTexelSize");
        internal static readonly int BlendNoise = Shader.PropertyToID("_TS_MeshBlendNoise");
        internal static readonly int BlendParameters = Shader.PropertyToID("_TS_MeshBlendParameters");
        internal static readonly int BlendExtra = Shader.PropertyToID("_TS_MeshBlendExtra");

        internal static readonly int[] Heights =
        {
            Shader.PropertyToID("_TS_BlendHeight0"),
            Shader.PropertyToID("_TS_BlendHeight1"),
            Shader.PropertyToID("_TS_BlendHeight2"),
            Shader.PropertyToID("_TS_BlendHeight3")
        };

        internal static readonly int[,] Controls = BuildControlIds();

        private static int[,] BuildControlIds()
        {
            int[,] ids = new int[MaximumBoundTiles, ControlsPerTile];
            for (int tile = 0; tile < MaximumBoundTiles; tile++)
            {
                for (int control = 0; control < ControlsPerTile; control++)
                {
                    ids[tile, control] = Shader.PropertyToID($"_TS_BlendControl{tile}{control}");
                }
            }
            return ids;
        }
    }
}
