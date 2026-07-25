#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{
internal sealed class TerrainPaintUndoTransaction
{
    private readonly HashSet<Object> registeredObjects = new();
    private int group = -1;
    private string undoName;

    public bool IsActive => group >= 0;
    internal int Group => group;

    public void Begin(string name)
    {
        if (IsActive)
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        group = Undo.GetCurrentGroup();
        undoName = name;
        Undo.SetCurrentGroupName(name);
        registeredObjects.Clear();
    }

    public void Register(Object target)
    {
        if (!IsActive || target == null || !registeredObjects.Add(target))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(target, undoName);
    }

    public void Complete()
    {
        if (!IsActive)
        {
            return;
        }

        Undo.CollapseUndoOperations(group);
        Reset();
    }

    public void Revert()
    {
        if (!IsActive)
        {
            return;
        }

        Undo.RevertAllDownToGroup(group);
        Reset();
    }

    private void Reset()
    {
        group = -1;
        undoName = null;
        registeredObjects.Clear();
    }
}
}
#endif
