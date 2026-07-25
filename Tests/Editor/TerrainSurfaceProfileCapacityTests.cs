using System.Collections.Generic;
using System.Reflection;
using Dominikozi.TerrainTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TerrainSurfaceProfileCapacityTests
{
    [Test]
    public void StochasticSampling_IsGloballyDisabledByDefault_ButAvailableToNewLayers()
    {
        TerrainSurfaceProfile profile = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
        try
        {
            TerrainSurfaceLayerSettings layer = new TerrainSurfaceLayerSettings(null);

            Assert.That(profile.StochasticSampling.Enabled, Is.False);
            Assert.That(layer.StochasticSampling, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [TestCase(0, 0)]
    [TestCase(1, 12)]
    [TestCase(11, 12)]
    [TestCase(12, 12)]
    [TestCase(13, 16)]
    [TestCase(15, 16)]
    [TestCase(16, 16)]
    [TestCase(17, 20)]
    [TestCase(19, 20)]
    [TestCase(20, 20)]
    [TestCase(21, 0)]
    public void GetShaderLayerCapacity_UsesAutomaticBuckets(int actualLayerCount, int expectedCapacity)
    {
        TerrainSurfaceProfile profile = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
        try
        {
            Assert.That(profile.GetShaderLayerCapacity(actualLayerCount), Is.EqualTo(expectedCapacity));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void BeginContextRendering_RestoresClearedProfileArraysAndTerrainControls()
    {
        Shader shader = Shader.Find("Terrain Tools/Terrain Surface Lit");
        Assert.That(shader, Is.Not.Null);

        TerrainSurfaceProfile profile = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
        TerrainLayer layer = new TerrainLayer
        {
            tileSize = new Vector2(8f, 4f),
            tileOffset = new Vector2(2f, 1f)
        };
        Texture2DArray albedoHeight = CreateArray();
        Texture2DArray normalSurface = CreateArray();
        Texture2DArray metallic = CreateArray();
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = 33,
            alphamapResolution = 16,
            size = new Vector3(32f, 8f, 32f),
            terrainLayers = new[] { layer }
        };
        Material material = new Material(shader);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        GameObject groupObject = new GameObject("Terrain Surface Render Binding Test");
        TerrainSurfaceGroup group = groupObject.AddComponent<TerrainSurfaceGroup>();

        try
        {
            profile.SynchronizeLayers(new[] { layer });
            profile.AssignGeneratedArrays(albedoHeight, normalSurface, metallic);
            group.SetTerrains(new[] { terrainObject.GetComponent<Terrain>() });
            group.SetGeneratedSetup(profile, material);

            int tilingId = Shader.PropertyToID("_TS_LayerTiling");
            int controlId = Shader.PropertyToID("_TS_Control0");
            int globalTintId = Shader.PropertyToID("_TS_GlobalTint");
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            Texture2D destroyedGlobalTint = new Texture2D(1, 1);
            SetPrivateField(profile.GlobalTexturing, "globalTint", destroyedGlobalTint);
            Object.DestroyImmediate(destroyedGlobalTint);
            terrain.SetSplatMaterialPropertyBlock(null);

            InvokeBeginContextRendering(group);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            terrain.GetSplatMaterialPropertyBlock(block);
            Vector4[] restoredTiling = block.GetVectorArray(tilingId);
            Assert.That(restoredTiling, Has.Length.EqualTo(TerrainSurfaceProfile.MaximumShaderLayerCapacity));
            Assert.That(restoredTiling[0], Is.EqualTo(new Vector4(0.125f, 0.25f, 0.25f, 0.25f)));
            Assert.That(block.GetTexture(controlId), Is.SameAs(terrainData.GetAlphamapTexture(0)));
            Assert.That(block.GetTexture(globalTintId), Is.SameAs(Texture2D.grayTexture));
        }
        finally
        {
            Object.DestroyImmediate(groupObject);
            Object.DestroyImmediate(terrainObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(terrainData);
            Object.DestroyImmediate(albedoHeight);
            Object.DestroyImmediate(normalSurface);
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(layer);
            Object.DestroyImmediate(profile);
        }
    }

    private static Texture2DArray CreateArray()
    {
        return new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, mipChain: false);
    }

    private static void InvokeBeginContextRendering(TerrainSurfaceGroup group)
    {
        MethodInfo callback = typeof(TerrainSurfaceGroup).GetMethod(
            "OnBeginContextRendering",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null);
        callback.Invoke(
            group,
            new object[]
            {
                default(ScriptableRenderContext),
                new List<Camera>()
            });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
