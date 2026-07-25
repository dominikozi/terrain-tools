using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

[CreateAssetMenu(
    fileName = "TerrainTreePaintPreset",
    menuName = "Terrain Tools/Presets/Tree Paint Preset")]
public sealed class TerrainTreePaintPreset : ScriptableObject
{
    [SerializeField] private List<Entry> entries = new();

    public List<Entry> Entries => entries;

    [Serializable]
    public sealed class Entry
    {
        public bool enabled = true;

        [Tooltip("Tree prefab to resolve against the TerrainData tree prototypes at paint time.")]
        public GameObject prefab;

        [Min(0f)]
        public float weight = 1f;

        public bool randomRotation = true;

        [Min(0.01f)]
        public float minHeightScale = 0.85f;

        [Min(0.01f)]
        public float maxHeightScale = 1.15f;

        public bool lockWidthToHeight = true;

        [Min(0.01f)]
        public float minWidthScale = 0.85f;

        [Min(0.01f)]
        public float maxWidthScale = 1.15f;
    }
}

}
