using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

[CreateAssetMenu(
    fileName = "DetailTerrainPaintPreset",
    menuName = "Terrain Tools/Presets/Detail Paint Preset")]
public sealed class DetailTerrainPaintPreset : ScriptableObject
{
    [SerializeField] private List<Entry> entries = new();

    public List<Entry> Entries => entries;

    [Serializable]
    public sealed class Entry
    {
        public bool enabled = true;

        [Tooltip("Detail mesh prefab. Assign either a prefab or a texture, never both.")]
        public GameObject prefab;

        [Tooltip("Detail texture. Assign either a texture or a prefab, never both.")]
        public Texture2D texture;

        [Min(0f)]
        public float weight = 1f;

        [Range(0f, 1f)]
        public float coverage = 1f;

        [Min(0.001f)]
        public float noiseScale = 12f;

        [Range(0f, 1f)]
        public float noiseInfluence = 1f;

        public int seed;
    }
}

}
