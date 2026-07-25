using System.Collections.Generic;
using System.Reflection;
using Dominikozi.TerrainTools.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
public sealed class TerrainSurfaceProceduralEvaluatorTests
{
    [Test]
    public void NoRules_AssignsEveryTexelToFallbackLayer()
    {
        using ProceduralFixture fixture = new ProceduralFixture();
        SetPrivateField(fixture.Profile, "fallbackLayer", fixture.Layer0);

        using TerrainSurfaceProceduralEvaluator evaluator =
            new TerrainSurfaceProceduralEvaluator(fixture.Group, fixture.Profile);
        float[,,] result = evaluator.EvaluateTerrain(fixture.Terrain, 4, 4);

        AssertEveryTexel(result, expectedLayer: 0);
    }

    [Test]
    public void HigherPriorityRule_ClaimsWeightBeforeLowerPriorityRule()
    {
        using ProceduralFixture fixture = new ProceduralFixture();
        TerrainSurfaceProceduralRule lower = CreateRule(fixture.Layer0, priority: 0);
        TerrainSurfaceProceduralRule higher = CreateRule(fixture.Layer1, priority: 10);
        SetPrivateField(fixture.Profile, "fallbackLayer", fixture.Layer0);
        SetPrivateField(
            fixture.Profile,
            "rules",
            new List<TerrainSurfaceProceduralRule> { lower, higher });

        using TerrainSurfaceProceduralEvaluator evaluator =
            new TerrainSurfaceProceduralEvaluator(fixture.Group, fixture.Profile);
        float[,,] result = evaluator.EvaluateTerrain(fixture.Terrain, 4, 4);

        AssertEveryTexel(result, expectedLayer: 1);
    }

    private static TerrainSurfaceProceduralRule CreateRule(TerrainLayer layer, int priority)
    {
        TerrainSurfaceProceduralRule rule = new TerrainSurfaceProceduralRule();
        SetPrivateField(rule, "targetLayer", layer);
        SetPrivateField(rule, "priority", priority);
        SetPrivateField(rule, "strength", 1f);
        return rule;
    }

    private static void AssertEveryTexel(float[,,] result, int expectedLayer)
    {
        for (int y = 0; y < result.GetLength(0); y++)
        {
            for (int x = 0; x < result.GetLength(1); x++)
            {
                float sum = 0f;
                for (int layer = 0; layer < result.GetLength(2); layer++)
                {
                    sum += result[y, x, layer];
                    Assert.That(
                        result[y, x, layer],
                        Is.EqualTo(layer == expectedLayer ? 1f : 0f).Within(0.00001f));
                }

                Assert.That(sum, Is.EqualTo(1f).Within(0.00001f));
            }
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private sealed class ProceduralFixture : System.IDisposable
    {
        private readonly GameObject terrainObject;
        private readonly GameObject groupObject;
        private readonly TerrainData terrainData;

        public ProceduralFixture()
        {
            Layer0 = new TerrainLayer();
            Layer1 = new TerrainLayer();
            terrainData = new TerrainData
            {
                heightmapResolution = 33,
                alphamapResolution = 16,
                size = new Vector3(32f, 8f, 32f),
                terrainLayers = new[] { Layer0, Layer1 }
            };
            terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            Terrain = terrainObject.GetComponent<Terrain>();
            groupObject = new GameObject("Procedural Evaluator Test Group");
            Group = groupObject.AddComponent<TerrainSurfaceGroup>();
            Group.SetTerrains(new[] { Terrain });
            Profile = ScriptableObject.CreateInstance<TerrainSurfaceProceduralProfile>();
        }

        public TerrainLayer Layer0 { get; }
        public TerrainLayer Layer1 { get; }
        public Terrain Terrain { get; }
        public TerrainSurfaceGroup Group { get; }
        public TerrainSurfaceProceduralProfile Profile { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(Profile);
            Object.DestroyImmediate(groupObject);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(terrainData);
            Object.DestroyImmediate(Layer0);
            Object.DestroyImmediate(Layer1);
        }
    }
}
}
