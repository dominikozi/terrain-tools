using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

[CreateAssetMenu(
    fileName = "CompositeTerrainPaintPreset",
    menuName = "Terrain Tools/Presets/Composite Layer Paint Preset")]
public sealed class CompositeTerrainPaintPreset : ScriptableObject
{
    [SerializeField] private List<Entry> entries = new();

    public List<Entry> Entries => entries;

    [Serializable]
    public sealed class Entry
    {
        public bool enabled = true;

        public TerrainLayer layer;

        [Min(0f)]
        public float weight = 1f;

        [Range(0f, 1f)]
        public float coverage = 1f;

        [Min(0.001f)]
        public float noiseScale = 16f;

        [Range(0f, 1f)]
        public float noiseInfluence = 1f;

        public int seed;
    }
}

}
