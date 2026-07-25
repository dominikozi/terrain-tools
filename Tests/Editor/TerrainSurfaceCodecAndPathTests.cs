using System;
using Dominikozi.TerrainTools.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
public sealed class TerrainSurfaceCodecAndPathTests
{
    [Test]
    public void AlphamapCodec_RoundTripsNormalizedWeights()
    {
        float[,,] source =
        {
            { { 0.1f, 0.2f, 0.7f }, { 0.5f, 0.25f, 0.25f } },
            { { 1f, 0f, 0f }, { 0f, 0.4f, 0.6f } }
        };

        byte[] encoded = TerrainSurfaceAlphamapCodec.Encode(source);
        float[,,] decoded = TerrainSurfaceAlphamapCodec.Decode(encoded, 2, 2, 3);

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                float sum = 0f;
                for (int layer = 0; layer < 3; layer++)
                {
                    sum += decoded[y, x, layer];
                    Assert.That(decoded[y, x, layer], Is.EqualTo(source[y, x, layer]).Within(1f / 127f));
                }

                Assert.That(sum, Is.EqualTo(1f).Within(0.00001f));
            }
        }
    }

    [Test]
    public void GeneratedPaths_AreProjectLocalAndOutsideThePackage()
    {
        Assert.That(TerrainToolsPaths.GeneratedRoot, Is.EqualTo("Assets/Generated/TerrainTools"));
        Assert.That(TerrainToolsPaths.TerrainSurfaceGeneratedRoot, Does.StartWith(TerrainToolsPaths.GeneratedRoot));
        Assert.That(TerrainToolsPaths.TerrainSurfaceGeneratedRoot, Does.Not.StartWith("Packages/"));
        Assert.That(TerrainToolsPaths.TerrainSurfaceGeneratedRoot, Does.Not.Contain("\\"));
        Assert.That(System.IO.Path.IsPathRooted(TerrainToolsPaths.TerrainSurfaceGeneratedRoot), Is.False);
    }

    [Test]
    public void ShaderAssetsAndLookups_AreResolvable()
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<Shader>(TerrainToolsAssetLocator.TerrainShaderPath),
            Is.Not.Null);
        Assert.That(
            AssetDatabase.LoadAssetAtPath<Shader>(TerrainToolsAssetLocator.MeshBlendShaderPath),
            Is.Not.Null);
        Assert.That(TerrainToolsAssetLocator.FindTerrainShader(), Is.Not.Null);
        Assert.That(TerrainToolsAssetLocator.FindMeshBlendShader(), Is.Not.Null);
    }
}
}
