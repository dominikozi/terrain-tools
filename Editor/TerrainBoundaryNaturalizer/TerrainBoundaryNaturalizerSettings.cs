using System;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    internal enum TerrainBoundaryLayerScope
    {
        Auto = 0,
        SelectedPair = 1
    }

    internal enum TerrainBoundaryCharacter
    {
        Clean = 0,
        Islands = 1
    }

    internal enum TerrainBoundaryIslandSource
    {
        LayerA = 0,
        LayerB = 1
    }

    [Serializable]
    internal sealed class TerrainBoundaryNaturalizerSettings
    {
        [SerializeField] private bool paintingEnabled = true;
        [SerializeField] private float brushDiameter = 30f;
        [SerializeField] private float brushFalloff = 0.7f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private TerrainBoundaryLayerScope layerScope = TerrainBoundaryLayerScope.Auto;
        [SerializeField] private TerrainBoundaryCharacter character = TerrainBoundaryCharacter.Clean;
        [SerializeField] private TerrainLayer layerA;
        [SerializeField] private TerrainLayer layerB;
        [SerializeField] private TerrainBoundaryIslandSource islandSource = TerrainBoundaryIslandSource.LayerA;
        [SerializeField] private float edgeContrast;

        [SerializeField] private float largeFeatureSize = 12f;
        [SerializeField] private float largeDisplacement = 2.5f;
        [SerializeField] private float mediumFeatureSize = 3f;
        [SerializeField] private float mediumDisplacement = 0.75f;
        [SerializeField] private float smallFeatureSize = 0.75f;
        [SerializeField] private float smallDisplacement = 0.18f;

        [SerializeField] private float islandSize = 0.6f;
        [SerializeField] private float islandReach = 1.5f;
        [SerializeField] private float islandAmount = 0.2f;

        public bool PaintingEnabled { get => paintingEnabled; set => paintingEnabled = value; }
        public float BrushDiameter { get => brushDiameter; set => brushDiameter = value; }
        public float BrushRadius => brushDiameter * 0.5f;
        public float BrushFalloff { get => brushFalloff; set => brushFalloff = value; }
        public int Seed { get => seed; set => seed = value; }
        public TerrainBoundaryLayerScope LayerScope { get => layerScope; set => layerScope = value; }
        public TerrainBoundaryCharacter Character { get => character; set => character = value; }
        public TerrainLayer LayerA { get => layerA; set => layerA = value; }
        public TerrainLayer LayerB { get => layerB; set => layerB = value; }
        public TerrainBoundaryIslandSource IslandSource { get => islandSource; set => islandSource = value; }
        public float EdgeContrast { get => edgeContrast; set => edgeContrast = value; }

        public float LargeFeatureSize { get => largeFeatureSize; set => largeFeatureSize = value; }
        public float LargeDisplacement { get => largeDisplacement; set => largeDisplacement = value; }
        public float MediumFeatureSize { get => mediumFeatureSize; set => mediumFeatureSize = value; }
        public float MediumDisplacement { get => mediumDisplacement; set => mediumDisplacement = value; }
        public float SmallFeatureSize { get => smallFeatureSize; set => smallFeatureSize = value; }
        public float SmallDisplacement { get => smallDisplacement; set => smallDisplacement = value; }

        public float IslandSize { get => islandSize; set => islandSize = value; }
        public float IslandReach { get => islandReach; set => islandReach = value; }
        public float IslandAmount { get => islandAmount; set => islandAmount = value; }
        public float MaximumDisplacement =>
            (largeDisplacement + mediumDisplacement + smallDisplacement) * 1.41421356f;

        public void Sanitize()
        {
            brushDiameter = Mathf.Clamp(brushDiameter, 1f, 256f);
            brushFalloff = Mathf.Clamp01(brushFalloff);
            edgeContrast = Mathf.Clamp01(edgeContrast);

            largeFeatureSize = Mathf.Clamp(largeFeatureSize, 2f, 64f);
            largeDisplacement = Mathf.Clamp(largeDisplacement, 0f, 5f);
            mediumFeatureSize = Mathf.Clamp(mediumFeatureSize, 0.5f, 16f);
            mediumDisplacement = Mathf.Clamp(mediumDisplacement, 0f, 2f);
            smallFeatureSize = Mathf.Clamp(smallFeatureSize, 0.1f, 4f);
            smallDisplacement = Mathf.Clamp(smallDisplacement, 0f, 0.5f);

            islandSize = Mathf.Clamp(islandSize, 0.1f, 4f);
            islandReach = Mathf.Clamp(islandReach, 0.1f, 5f);
            islandAmount = Mathf.Clamp01(islandAmount);
        }
    }
}
