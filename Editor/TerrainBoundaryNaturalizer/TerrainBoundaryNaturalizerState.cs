using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.BoundaryNaturalizer
{
    [FilePath(
        "ProjectSettings/TerrainBoundaryNaturalizerSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TerrainBoundaryNaturalizerState
        : ScriptableSingleton<TerrainBoundaryNaturalizerState>
    {
        [SerializeField] private TerrainBoundaryNaturalizerSettings settings = new();

        public TerrainBoundaryNaturalizerSettings Settings
        {
            get
            {
                settings ??= new TerrainBoundaryNaturalizerSettings();
                settings.Sanitize();
                return settings;
            }
        }

        public void SaveSettings()
        {
            Settings.Sanitize();
            Save(true);
        }
    }
}
