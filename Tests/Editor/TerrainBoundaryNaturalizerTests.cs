using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    public sealed class TerrainBoundaryNaturalizerTests
    {
        [Test]
        public void Process_WarpsBoundary_AndLeavesDistantInteriorUntouched()
        {
            using TerrainFixture fixture = TerrainFixture.Create(32, 2, 1f, CreateHalfWeights(32, 2));
            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LargeDisplacement = 4f;
            settings.MediumDisplacement = 0f;
            settings.SmallDisplacement = 0f;

            float[,,] result = ProcessFull(fixture, settings, 0, 1);

            Assert.That(result, Is.Not.Null);
            Assert.That(CountChanged(fixture.Snapshot.Weights, result), Is.GreaterThan(0));
            for (int y = 0; y < result.GetLength(0); y++)
            {
                for (int x = 0; x <= 5; x++)
                {
                    AssertPixelsEqual(fixture.Snapshot.Weights, result, y, x);
                }
                for (int x = 26; x < result.GetLength(1); x++)
                {
                    AssertPixelsEqual(fixture.Snapshot.Weights, result, y, x);
                }
            }
        }

        [Test]
        public void Process_IsDeterministicForSeed_AndChangesForDifferentSeed()
        {
            using TerrainFixture fixture = TerrainFixture.Create(32, 2, 1f, CreateHalfWeights(32, 2));
            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LargeDisplacement = 4f;
            settings.MediumDisplacement = 1.5f;
            settings.SmallDisplacement = 0f;

            float[,,] first = ProcessFull(fixture, settings, 0, 1);
            float[,,] repeated = ProcessFull(fixture, settings, 0, 1);
            settings.Seed++;
            float[,,] changedSeed = ProcessFull(fixture, settings, 0, 1);

            AssertMapsEqual(first, repeated);
            Assert.That(CountChanged(first, changedSeed), Is.GreaterThan(0));
        }

        [Test]
        public void SelectedPair_PreservesEveryOtherLayer_AndNormalizesWeights()
        {
            float[,,] weights = CreateHalfWeights(32, 3, 0.2f);
            using TerrainFixture fixture = TerrainFixture.Create(32, 3, 1f, weights);
            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LayerScope = TerrainBoundaryLayerScope.SelectedPair;
            settings.EdgeContrast = 0.65f;
            settings.LargeDisplacement = 4f;

            float[,,] result = ProcessFull(fixture, settings, 0, 1);

            Assert.That(result, Is.Not.Null);
            for (int y = 0; y < result.GetLength(0); y++)
            {
                for (int x = 0; x < result.GetLength(1); x++)
                {
                    Assert.That(result[y, x, 2], Is.EqualTo(weights[y, x, 2]).Within(0.00001f));
                    AssertNormalized(result, y, x);
                }
            }
        }

        [Test]
        public void AutoMode_HandlesTwentyLayersAndThreeWayJunction()
        {
            const int resolution = 32;
            float[,,] weights = new float[resolution, resolution, 20];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int layer = y > resolution / 2 ? 2 : x < resolution / 2 ? 0 : 1;
                    weights[y, x, layer] = 1f;
                }
            }

            using TerrainFixture fixture = TerrainFixture.Create(resolution, 20, 1f, weights);
            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LargeDisplacement = 3f;
            float[,,] result = ProcessFull(fixture, settings, -1, -1);

            Assert.That(result, Is.Not.Null);
            Assert.That(CountChanged(weights, result), Is.GreaterThan(0));
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    AssertNormalized(result, y, x);
                }
            }
        }

        [Test]
        public void CleanTopology_RemovesDetachedIsland_ButKeepsConnectedPeninsula()
        {
            const int size = 16;
            float[,,] original = CreateHalfWeights(size, 2);
            float[,,] candidate = (float[,,])original.Clone();
            SetDominant(candidate, 8, 13, 0);
            SetDominant(candidate, 8, 8, 0);
            SetDominant(candidate, 8, 9, 0);
            SetDominant(candidate, 8, 10, 0);
            int[,] originalDominant = TerrainBoundaryTopology.BuildDominantMap(original);

            TerrainBoundaryTopology.RemoveDetachedComponents(original, candidate, originalDominant);

            Assert.That(TerrainBoundaryWeightUtility.FindDominant(candidate, 8, 13), Is.EqualTo(1));
            Assert.That(TerrainBoundaryWeightUtility.FindDominant(candidate, 8, 8), Is.EqualTo(0));
            Assert.That(TerrainBoundaryWeightUtility.FindDominant(candidate, 8, 9), Is.EqualTo(0));
            Assert.That(TerrainBoundaryWeightUtility.FindDominant(candidate, 8, 10), Is.EqualTo(0));
        }

        [Test]
        public void Islands_SelectedPairOnlySpillsChosenSourceIntoTargetSide()
        {
            const int resolution = 64;
            const float metersPerTexel = 0.25f;
            float[,,] original = CreateHalfWeights(resolution, 2);
            using TerrainFixture fixture = TerrainFixture.Create(
                resolution,
                2,
                metersPerTexel,
                original);
            float[,] distance = CreateStraightBoundaryDistance(resolution, metersPerTexel);
            float[,] brush = CreateFilledMask(resolution);
            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LayerScope = TerrainBoundaryLayerScope.SelectedPair;
            settings.Character = TerrainBoundaryCharacter.Islands;
            settings.IslandSource = TerrainBoundaryIslandSource.LayerA;
            settings.IslandAmount = 1f;
            settings.IslandReach = 4f;
            settings.IslandSize = 1.5f;

            float[,,] candidate = (float[,,])original.Clone();
            TerrainBoundaryTopology.AddIslands(
                fixture.Snapshot,
                candidate,
                distance,
                brush,
                settings,
                0,
                1);

            int middle = resolution / 2;
            int islandPixels = 0;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    if (x < middle)
                    {
                        Assert.That(
                            TerrainBoundaryWeightUtility.FindDominant(candidate, y, x),
                            Is.EqualTo(0));
                    }
                    else if (TerrainBoundaryWeightUtility.FindDominant(candidate, y, x) == 0)
                    {
                        islandPixels++;
                    }
                }
            }

            Assert.That(islandPixels, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void EdgeContrast_ZeroPreservesPair_AndHigherValueSharpensIt()
        {
            float[] unchanged = { 0.6f, 0.4f };
            TerrainBoundaryWeightUtility.ApplyPairContrast(unchanged, 0, 1, 0f);
            Assert.That(unchanged[0], Is.EqualTo(0.6f).Within(0.000001f));
            Assert.That(unchanged[1], Is.EqualTo(0.4f).Within(0.000001f));

            float[] sharpened = { 0.6f, 0.4f };
            TerrainBoundaryWeightUtility.ApplyPairContrast(sharpened, 0, 1, 1f);
            Assert.That(sharpened[0], Is.GreaterThan(0.6f));
            Assert.That(sharpened[1], Is.LessThan(0.4f));

            float[] midpoint = { 0.5f, 0.5f };
            TerrainBoundaryWeightUtility.ApplyPairContrast(midpoint, 0, 1, 1f);
            Assert.That(midpoint[0], Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(midpoint[1], Is.EqualTo(0.5f).Within(0.000001f));
        }

        [Test]
        public void StrokeMask_UsesPolylineInsteadOfAccumulatingOverlappingDabs()
        {
            TerrainBoundaryStroke straight = new();
            straight.AddPoint(new Vector3(0f, 0f, 0f));
            straight.AddPoint(new Vector3(10f, 0f, 0f));
            TerrainBoundaryStroke overlapping = new();
            overlapping.AddPoint(new Vector3(0f, 0f, 0f));
            overlapping.AddPoint(new Vector3(10f, 0f, 0f));
            overlapping.AddPoint(new Vector3(0f, 0f, 0f));
            overlapping.AddPoint(new Vector3(10f, 0f, 0f));

            Vector2 sample = new(5f, 1.5f);
            Assert.That(
                overlapping.EvaluateMask(sample, 3f, 0.7f),
                Is.EqualTo(straight.EvaluateMask(sample, 3f, 0.7f)).Within(0.000001f));
        }

        [Test]
        public void AdjacentTiles_ProduceIdenticalWeightsAlongSharedSeam()
        {
            const int resolution = 32;
            const float metersPerTexel = 1f;
            float[,,] weights = CreateZHalfWeights(resolution, 2);
            using TerrainFixture left = TerrainFixture.Create(
                resolution,
                2,
                metersPerTexel,
                weights);
            using TerrainFixture right = TerrainFixture.Create(
                resolution,
                2,
                metersPerTexel,
                weights);
            float tileSize = left.Terrain.terrainData.size.x;
            right.Terrain.transform.position = new Vector3(tileSize, 0f, 0f);

            TerrainBoundaryNaturalizerSettings settings = CreateSettings();
            settings.LargeDisplacement = 4f;
            TerrainBoundaryStroke stroke = new();
            float boundaryZ = left.Terrain.terrainData.size.z * 0.5f;
            stroke.AddPoint(new Vector3(0f, 0f, boundaryZ));
            stroke.AddPoint(new Vector3(tileSize * 2f, 0f, boundaryZ));
            TerrainBoundaryWorldSampler sampler = new(new[] { left.Snapshot, right.Snapshot });

            float[,,] leftResult = ProcessFull(left, sampler, stroke, settings, -1, -1);
            float[,,] rightResult = ProcessFull(right, sampler, stroke, settings, -1, -1);

            Assert.That(leftResult, Is.Not.Null);
            Assert.That(rightResult, Is.Not.Null);
            Assert.That(CountChanged(weights, leftResult), Is.GreaterThan(0));
            Assert.That(CountChanged(weights, rightResult), Is.GreaterThan(0));
            for (int y = 0; y < resolution; y++)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    Assert.That(
                        rightResult[y, 0, layer],
                        Is.EqualTo(leftResult[y, resolution - 1, layer]).Within(0.000001f));
                }
            }
        }

        [Test]
        public void ApplyResults_UndoRestoresAlphamap_AndRedoReappliesIt()
        {
            const int resolution = 16;
            float[,,] original = CreateHalfWeights(resolution, 2);
            using TerrainFixture fixture = TerrainFixture.Create(resolution, 2, 1f, original);
            float[,,] changed = (float[,,])original.Clone();
            changed[resolution / 2, resolution / 2, 0] = 0.75f;
            changed[resolution / 2, resolution / 2, 1] = 0.25f;
            TerrainBoundaryTileResult result = new(
                fixture.Terrain,
                0,
                0,
                changed,
                1);

            TerrainBoundaryNaturalizerService.ApplyResults(new[] { result });
            AssertMapsEqual(changed, fixture.Terrain.terrainData.GetAlphamaps(
                0,
                0,
                resolution,
                resolution));

            Undo.PerformUndo();
            fixture.Terrain.Flush();
            AssertMapsEqual(original, fixture.Terrain.terrainData.GetAlphamaps(
                0,
                0,
                resolution,
                resolution));

            Undo.PerformRedo();
            fixture.Terrain.Flush();
            AssertMapsEqual(changed, fixture.Terrain.terrainData.GetAlphamaps(
                0,
                0,
                resolution,
                resolution));
        }

        private static TerrainBoundaryNaturalizerSettings CreateSettings()
        {
            TerrainBoundaryNaturalizerSettings settings = new()
            {
                BrushDiameter = 1000f,
                BrushFalloff = 0f,
                LayerScope = TerrainBoundaryLayerScope.Auto,
                Character = TerrainBoundaryCharacter.Clean,
                EdgeContrast = 0f,
                Seed = 12345,
                LargeFeatureSize = 12f,
                LargeDisplacement = 2.5f,
                MediumFeatureSize = 3f,
                MediumDisplacement = 0.75f,
                SmallFeatureSize = 0.75f,
                SmallDisplacement = 0.18f
            };
            return settings;
        }

        private static float[,,] ProcessFull(
            TerrainFixture fixture,
            TerrainBoundaryNaturalizerSettings settings,
            int layerA,
            int layerB)
        {
            TerrainBoundaryStroke stroke = new();
            float center = fixture.Terrain.terrainData.size.x * 0.5f;
            stroke.AddPoint(fixture.Terrain.transform.position + new Vector3(center, 0f, center));
            TerrainBoundaryWorldSampler sampler = new(new[] { fixture.Snapshot });
            return ProcessFull(fixture, sampler, stroke, settings, layerA, layerB);
        }

        private static float[,,] ProcessFull(
            TerrainFixture fixture,
            TerrainBoundaryWorldSampler sampler,
            TerrainBoundaryStroke stroke,
            TerrainBoundaryNaturalizerSettings settings,
            int layerA,
            int layerB)
        {
            TerrainBoundaryTileResult result = TerrainBoundaryNaturalizerProcessor.Process(
                fixture.Snapshot,
                sampler,
                stroke,
                settings,
                layerA,
                layerB);
            if (result == null)
            {
                return null;
            }

            float[,,] full = (float[,,])fixture.Snapshot.Weights.Clone();
            int localStartX = result.X - fixture.Snapshot.Rect.X;
            int localStartY = result.Y - fixture.Snapshot.Rect.Y;
            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    for (int layer = 0; layer < full.GetLength(2); layer++)
                    {
                        full[localStartY + y, localStartX + x, layer] = result.Weights[y, x, layer];
                    }
                }
            }

            return full;
        }

        private static float[,,] CreateHalfWeights(int resolution, int layers, float thirdLayerWeight = 0f)
        {
            float[,,] result = new float[resolution, resolution, layers];
            float primaryWeight = 1f - thirdLayerWeight;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    result[y, x, x < resolution / 2 ? 0 : 1] = primaryWeight;
                    if (layers > 2)
                    {
                        result[y, x, 2] = thirdLayerWeight;
                    }
                }
            }

            return result;
        }

        private static float[,,] CreateZHalfWeights(int resolution, int layers)
        {
            float[,,] result = new float[resolution, resolution, layers];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    result[y, x, y < resolution / 2 ? 0 : 1] = 1f;
                }
            }

            return result;
        }

        private static float[,] CreateStraightBoundaryDistance(int resolution, float metersPerTexel)
        {
            float[,] result = new float[resolution, resolution];
            int leftEdge = resolution / 2 - 1;
            int rightEdge = resolution / 2;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int texelDistance = x < resolution / 2 ? leftEdge - x : x - rightEdge;
                    result[y, x] = Mathf.Max(0, texelDistance) * metersPerTexel;
                }
            }

            return result;
        }

        private static float[,] CreateFilledMask(int resolution)
        {
            float[,] result = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    result[y, x] = 1f;
                }
            }
            return result;
        }

        private static void SetDominant(float[,,] weights, int y, int x, int layer)
        {
            for (int index = 0; index < weights.GetLength(2); index++)
            {
                weights[y, x, index] = index == layer ? 1f : 0f;
            }
        }

        private static int CountChanged(float[,,] first, float[,,] second)
        {
            if (first == null || second == null)
            {
                return first == second ? 0 : int.MaxValue;
            }

            int changed = 0;
            for (int y = 0; y < first.GetLength(0); y++)
            {
                for (int x = 0; x < first.GetLength(1); x++)
                {
                    if (TerrainBoundaryWeightUtility.PixelsDiffer(first, second, y, x, 0.00001f))
                    {
                        changed++;
                    }
                }
            }
            return changed;
        }

        private static void AssertPixelsEqual(float[,,] first, float[,,] second, int y, int x)
        {
            for (int layer = 0; layer < first.GetLength(2); layer++)
            {
                Assert.That(second[y, x, layer], Is.EqualTo(first[y, x, layer]).Within(0.000001f));
            }
        }

        private static void AssertMapsEqual(float[,,] first, float[,,] second)
        {
            Assert.That(second, Is.Not.Null);
            for (int y = 0; y < first.GetLength(0); y++)
            {
                for (int x = 0; x < first.GetLength(1); x++)
                {
                    AssertPixelsEqual(first, second, y, x);
                }
            }
        }

        private static void AssertNormalized(float[,,] weights, int y, int x)
        {
            float sum = 0f;
            for (int layer = 0; layer < weights.GetLength(2); layer++)
            {
                Assert.That(weights[y, x, layer], Is.GreaterThanOrEqualTo(0f));
                sum += weights[y, x, layer];
            }
            Assert.That(sum, Is.EqualTo(1f).Within(0.00001f));
        }

        private sealed class TerrainFixture : IDisposable
        {
            private readonly TerrainData data;
            private readonly TerrainLayer[] layers;
            private readonly GameObject terrainObject;

            public Terrain Terrain { get; }
            public TerrainBoundaryTileSnapshot Snapshot { get; }

            private TerrainFixture(
                TerrainData terrainData,
                TerrainLayer[] terrainLayers,
                GameObject gameObject,
                Terrain terrain,
                TerrainBoundaryTileSnapshot snapshot)
            {
                data = terrainData;
                layers = terrainLayers;
                terrainObject = gameObject;
                Terrain = terrain;
                Snapshot = snapshot;
            }

            public static TerrainFixture Create(
                int resolution,
                int layerCount,
                float metersPerTexel,
                float[,,] weights)
            {
                TerrainData data = new()
                {
                    alphamapResolution = resolution,
                    heightmapResolution = 33,
                    size = new Vector3(
                        (resolution - 1) * metersPerTexel,
                        10f,
                        (resolution - 1) * metersPerTexel)
                };
                TerrainLayer[] layers = new TerrainLayer[layerCount];
                for (int i = 0; i < layers.Length; i++)
                {
                    layers[i] = new TerrainLayer();
                    layers[i].name = $"Layer {i}";
                }
                data.terrainLayers = layers;
                data.SetAlphamaps(0, 0, weights);
                GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
                Terrain terrain = terrainObject.GetComponent<Terrain>();
                TerrainBoundaryGridRect rect = new(
                    0,
                    0,
                    data.alphamapWidth,
                    data.alphamapHeight);
                TerrainBoundaryTileSnapshot snapshot = new(
                    terrain,
                    rect,
                    data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight));
                return new TerrainFixture(data, layers, terrainObject, terrain, snapshot);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(terrainObject);
                for (int i = 0; i < layers.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(layers[i]);
                }
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
