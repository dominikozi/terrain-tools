using Dominikozi.TerrainTools.Editor.Painters;
using NUnit.Framework;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
public sealed class TerrainPrototypeResolverTests
{
    [Test]
    public void TreePrefab_ResolvesByAssetReference()
    {
        TerrainData data = new TerrainData();
        GameObject oak = new GameObject("Oak");
        try
        {
            data.treePrototypes = new[] { new TreePrototype { prefab = oak } };

            bool resolved = TerrainPrototypeResolver.TryResolveTree(
                data,
                oak,
                out int index,
                out string error);

            Assert.That(resolved, Is.True, error);
            Assert.That(index, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(oak);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void TreePrefab_DuplicatePrototypeIsRejected()
    {
        TerrainData data = new TerrainData();
        GameObject oak = new GameObject("Oak");
        try
        {
            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = oak },
                new TreePrototype { prefab = oak }
            };

            bool resolved = TerrainPrototypeResolver.TryResolveTree(
                data,
                oak,
                out int index,
                out string error);

            Assert.That(resolved, Is.False);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(error, Does.Contain("occurs 2 times"));
        }
        finally
        {
            Object.DestroyImmediate(oak);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void DetailTexture_MissingAndAmbiguousSourcesAreRejected()
    {
        TerrainData data = new TerrainData();
        Texture2D grass = new Texture2D(1, 1);
        try
        {
            data.detailPrototypes = new[]
            {
                new DetailPrototype { prototypeTexture = grass },
                new DetailPrototype { prototypeTexture = grass }
            };

            Assert.That(
                TerrainPrototypeResolver.TryResolveDetail(
                    data,
                    null,
                    grass,
                    out _,
                    out string duplicateError),
                Is.False);
            Assert.That(duplicateError, Does.Contain("occurs 2 times"));

            Assert.That(
                TerrainPrototypeResolver.TryResolveDetail(
                    data,
                    null,
                    null,
                    out _,
                    out string missingError),
                Is.False);
            Assert.That(missingError, Does.Contain("exactly one"));
        }
        finally
        {
            Object.DestroyImmediate(grass);
            Object.DestroyImmediate(data);
        }
    }
}
}
