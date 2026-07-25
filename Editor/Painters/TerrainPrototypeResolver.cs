#if UNITY_EDITOR
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal static class TerrainPrototypeResolver
{
    public static bool TryResolveTree(
        TerrainData terrainData,
        GameObject prefab,
        out int prototypeIndex,
        out string error)
    {
        prototypeIndex = -1;
        if (terrainData == null)
        {
            error = "The target has no TerrainData.";
            return false;
        }

        if (prefab == null)
        {
            error = "Assign a tree prefab to every enabled preset entry.";
            return false;
        }

        TreePrototype[] prototypes = terrainData.treePrototypes;
        int matches = 0;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prefab != prefab)
            {
                continue;
            }

            prototypeIndex = i;
            matches++;
        }

        if (matches == 1)
        {
            error = null;
            return true;
        }

        prototypeIndex = -1;
        error = matches == 0
            ? $"Tree prefab '{prefab.name}' is not registered on the target TerrainData."
            : $"Tree prefab '{prefab.name}' occurs {matches} times on the target TerrainData. Remove duplicate prototypes before painting.";
        return false;
    }

    public static bool TryResolveDetail(
        TerrainData terrainData,
        GameObject prefab,
        Texture2D texture,
        out int prototypeIndex,
        out string error)
    {
        prototypeIndex = -1;
        if (terrainData == null)
        {
            error = "The target has no TerrainData.";
            return false;
        }

        if ((prefab == null) == (texture == null))
        {
            error = "Assign exactly one detail source: a prefab or a texture.";
            return false;
        }

        DetailPrototype[] prototypes = terrainData.detailPrototypes;
        int matches = 0;
        for (int i = 0; i < prototypes.Length; i++)
        {
            bool matchesSource = prefab != null
                ? prototypes[i].prototype == prefab
                : prototypes[i].prototypeTexture == texture;
            if (!matchesSource)
            {
                continue;
            }

            prototypeIndex = i;
            matches++;
        }

        if (matches == 1)
        {
            error = null;
            return true;
        }

        Object source = prefab != null ? prefab : texture;
        prototypeIndex = -1;
        error = matches == 0
            ? $"Detail source '{source.name}' is not registered on the target TerrainData."
            : $"Detail source '{source.name}' occurs {matches} times on the target TerrainData. Remove duplicate prototypes before painting.";
        return false;
    }
}
}
#endif
