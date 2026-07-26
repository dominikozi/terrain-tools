using System.Collections.Generic;
using Dominikozi.TerrainTools.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
    public sealed class TerrainDataTransferServiceTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TerrainLayerMap_FollowsSourceOrder()
        {
            TerrainLayer grass = CreateTerrainLayer();
            TerrainLayer rock = CreateTerrainLayer();

            int[] map = TerrainDataTransferService.BuildTerrainLayerMap(
                new[] { rock, grass },
                new[] { grass, rock });

            Assert.That(map, Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void TerrainLayerMap_MatchesDuplicateAssetsByOccurrence()
        {
            TerrainLayer grass = CreateTerrainLayer();
            TerrainLayer rock = CreateTerrainLayer();

            int[] map = TerrainDataTransferService.BuildTerrainLayerMap(
                new[] { grass, grass, rock },
                new[] { grass, rock, grass });

            Assert.That(map, Is.EqualTo(new[] { 0, 2, 1 }));
        }

        [Test]
        public void DetailPrototypeMap_MatchesAssetDespiteSettingDifferences()
        {
            Texture2D grass = CreateTexture();
            Texture2D flowers = CreateTexture();
            DetailPrototype sourceGrass = new()
            {
                prototypeTexture = grass,
                density = 0.25f
            };
            DetailPrototype sourceFlowers = new()
            {
                prototypeTexture = flowers
            };
            DetailPrototype targetGrass = new()
            {
                prototypeTexture = grass,
                density = 0.9f
            };

            int[] map = TerrainDataTransferService.BuildDetailPrototypeMap(
                new[] { sourceGrass, sourceFlowers },
                new[] { targetGrass });

            Assert.That(map, Is.EqualTo(new[] { 0, -1 }));
        }

        [Test]
        public void TreePrototypeMap_MatchesDuplicatePrefabsByOccurrence()
        {
            GameObject oak = CreateGameObject("Oak");
            GameObject pine = CreateGameObject("Pine");

            int[] map = TerrainDataTransferService.BuildTreePrototypeMap(
                new[]
                {
                    new TreePrototype { prefab = oak },
                    new TreePrototype { prefab = oak },
                    new TreePrototype { prefab = pine }
                },
                new[]
                {
                    new TreePrototype { prefab = oak },
                    new TreePrototype { prefab = pine },
                    new TreePrototype { prefab = oak }
                });

            Assert.That(map, Is.EqualTo(new[] { 0, 2, 1 }));
        }

        [Test]
        public void RemapAlphamaps_ReordersDropsAndNormalizesWeights()
        {
            float[,,] oldAlphamaps = new float[1, 1, 3];
            oldAlphamaps[0, 0, 0] = 0.2f;
            oldAlphamaps[0, 0, 1] = 0.3f;
            oldAlphamaps[0, 0, 2] = 0.5f;

            float[,,] result = TerrainDataTransferService.RemapAlphamaps(
                oldAlphamaps,
                1,
                1,
                new[] { 2, 0 });

            Assert.That(result[0, 0, 0], Is.EqualTo(5f / 7f).Within(0.0001f));
            Assert.That(result[0, 0, 1], Is.EqualTo(2f / 7f).Within(0.0001f));
        }

        [Test]
        public void RemapAlphamaps_DefaultsToFirstLayerWhenNothingMatches()
        {
            float[,,] result = TerrainDataTransferService.RemapAlphamaps(
                null,
                2,
                1,
                new[] { -1, -1 });

            Assert.That(result[0, 0, 0], Is.EqualTo(1f));
            Assert.That(result[0, 0, 1], Is.EqualTo(0f));
            Assert.That(result[0, 1, 0], Is.EqualTo(1f));
            Assert.That(result[0, 1, 1], Is.EqualTo(0f));
        }

        private TerrainLayer CreateTerrainLayer()
        {
            TerrainLayer layer = new();
            createdObjects.Add(layer);
            return layer;
        }

        private Texture2D CreateTexture()
        {
            Texture2D texture = new(1, 1);
            createdObjects.Add(texture);
            return texture;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
