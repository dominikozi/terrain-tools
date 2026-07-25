using Dominikozi.TerrainTools.Editor.Painters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Tests.Editor
{
public sealed class TerrainPaintUndoTransactionTests
{
    [Test]
    public void MultipleChangesInAStroke_UndoAsOneOperation()
    {
        TerrainData data = new TerrainData { size = new Vector3(10f, 10f, 10f) };
        try
        {
            TerrainPaintUndoTransaction transaction = new TerrainPaintUndoTransaction();
            transaction.Begin("Test Terrain Stroke");
            int group = transaction.Group;
            transaction.Register(data);
            data.size = new Vector3(20f, 10f, 10f);
            transaction.Register(data);
            data.size = new Vector3(30f, 10f, 10f);
            transaction.Complete();

            Assert.That(group, Is.GreaterThanOrEqualTo(0));
            Assert.That(transaction.IsActive, Is.False);

            Undo.PerformUndo();
            Assert.That(data.size.x, Is.EqualTo(10f));
        }
        finally
        {
            Undo.ClearAll();
            Object.DestroyImmediate(data);
        }
    }
}
}
