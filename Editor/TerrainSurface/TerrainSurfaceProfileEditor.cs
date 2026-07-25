using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [CustomEditor(typeof(TerrainSurfaceProfile))]
    internal sealed class TerrainSurfaceProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty layers;
        private SerializedProperty albedoHeightArray;
        private SerializedProperty normalSurfaceArray;
        private SerializedProperty metallicArray;
        private SerializedProperty antiTiling;
        private SerializedProperty stochasticSampling;
        private SerializedProperty globalTexturing;

        private void OnEnable()
        {
            layers = serializedObject.FindProperty("layers");
            albedoHeightArray = serializedObject.FindProperty("albedoHeightArray");
            normalSurfaceArray = serializedObject.FindProperty("normalSurfaceArray");
            metallicArray = serializedObject.FindProperty("metallicArray");
            antiTiling = serializedObject.FindProperty("antiTiling");
            stochasticSampling = serializedObject.FindProperty("stochasticSampling");
            globalTexturing = serializedObject.FindProperty("globalTexturing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "layers",
                "albedoHeightArray",
                "normalSurfaceArray",
                "metallicArray",
                "antiTiling",
                "stochasticSampling",
                "globalTexturing");

            DrawActiveEffectsSummary();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional Modules", EditorStyles.boldLabel);
            DrawModule(antiTiling, "Anti Tiling");
            DrawModule(stochasticSampling, "Stochastic Sampling");
            EditorGUILayout.HelpBox(
                "Stochastic Sampling Enabled is an independent master switch. It affects only Terrain Layers " +
                "whose per-layer Stochastic Sampling checkbox is also enabled.",
                MessageType.None);
            DrawModule(globalTexturing, "Global Texturing");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generated Arrays", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(albedoHeightArray);
                EditorGUILayout.PropertyField(normalSurfaceArray);
                EditorGUILayout.PropertyField(metallicArray);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Terrain Layers ({layers.arraySize})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Layer references and their order are synchronized from TerrainData. Configure the parameters here; " +
                "use the TerrainSurfaceGroup inspector to change or rebuild the ordered layer list.",
                MessageType.Info);
            for (int i = 0; i < layers.arraySize; i++)
            {
                DrawLayer(layers.GetArrayElementAtIndex(i), i);
            }
            TerrainSurfaceProfile profile = (TerrainSurfaceProfile)target;
            if (serializedObject.ApplyModifiedProperties())
            {
                ApplyProfileToLoadedObjects(profile);
            }

            TerrainSurfaceGroup matchingGroup = FindMatchingGroup(profile);
            using (new EditorGUI.DisabledScope(matchingGroup == null))
            {
                if (GUILayout.Button("Build / Rebuild Arrays For Matching Group"))
                {
                    bool succeeded = TerrainSurfaceTextureArrayBuilder.Build(matchingGroup, out string message);
                    EditorUtility.DisplayDialog(
                        succeeded ? "Terrain Surface Arrays" : "Terrain Surface Array Error",
                        message,
                        "OK");
                }
            }
            if (matchingGroup == null)
            {
                EditorGUILayout.HelpBox(
                    "No loaded TerrainSurfaceGroup currently references this profile.",
                    MessageType.None);
            }
        }

        private void DrawActiveEffectsSummary()
        {
            bool heightBlendEnabled = serializedObject.FindProperty("heightBlendEnabled").boolValue;
            int triplanarLayerCount = 0;
            int stochasticLayerCount = 0;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.FindPropertyRelative("triplanar").boolValue)
                {
                    triplanarLayerCount++;
                }
                if (layer.FindPropertyRelative("stochasticSampling").boolValue)
                {
                    stochasticLayerCount++;
                }
            }

            string summary =
                "Core renderer: texture arrays + dominant-layer selection. " +
                $"Height blend: {(heightBlendEnabled ? "ON" : "OFF")}. " +
                $"Anti Tiling: {EnabledLabel(antiTiling)}. " +
                $"Stochastic Sampling: {EnabledLabel(stochasticSampling)} ({stochasticLayerCount} layer(s)). " +
                $"Global Texturing: {EnabledLabel(globalTexturing)}. " +
                $"Triplanar layers: {triplanarLayerCount}.";
            EditorGUILayout.HelpBox(
                summary +
                "\nWhen optional modules and triplanar are OFF, layer albedo, normals and Mask Map values " +
                "follow URP TerrainLit. Height blend can still reveal fine detail from Mask Map B at layer transitions." +
                "\nURP TerrainLit disables its own height blend when a Terrain has more than four layers. " +
                "For an A/B baseline against such a Terrain, turn Height Blend OFF here; turn it back ON to test " +
                "the multi-layer height blend provided by this system.",
                MessageType.Info);
        }

        private static string EnabledLabel(SerializedProperty module)
        {
            return module.FindPropertyRelative("enabled").boolValue ? "ON" : "OFF";
        }

        private static void DrawModule(SerializedProperty module, string label)
        {
            SerializedProperty enabled = module.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent($"{label} Enabled"));
            module.isExpanded = EditorGUILayout.Foldout(
                module.isExpanded,
                $"{label} Settings ({(enabled.boolValue ? "Active" : "Inactive")})",
                toggleOnLabelClick: true);
            if (!module.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
            {
                SerializedProperty child = module.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    if (child.name != "enabled")
                    {
                        EditorGUILayout.PropertyField(child, includeChildren: true);
                    }
                }
            }
        }

        private static void DrawLayer(SerializedProperty layer, int index)
        {
            SerializedProperty terrainLayer = layer.FindPropertyRelative("terrainLayer");
            string label = terrainLayer.objectReferenceValue != null
                ? $"{index}: {terrainLayer.objectReferenceValue.name}"
                : $"{index}: Missing TerrainLayer";
            layer.isExpanded = EditorGUILayout.Foldout(layer.isExpanded, label, toggleOnLabelClick: true);
            if (!layer.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(terrainLayer);
                }
                DrawRelative(layer, "heightOffset");
                DrawRelative(layer, "heightContrast");
                DrawRelative(layer, "normalStrength");
                DrawRelative(layer, "metallicMultiplier");
                DrawRelative(layer, "smoothnessMultiplier");
                DrawRelative(layer, "ambientOcclusionStrength");
                DrawRelative(layer, "detailNoiseStrength");
                DrawRelative(layer, "macroNoiseStrength");
                DrawRelative(layer, "normalNoiseStrength");
                DrawRelative(layer, "distanceResampleStrength");
                DrawRelative(layer, "stochasticSampling");
                DrawRelative(layer, "triplanar");
                if (layer.FindPropertyRelative("triplanar").boolValue)
                {
                    DrawRelative(layer, "triplanarScale");
                    DrawRelative(layer, "triplanarSharpness");
                    DrawRelative(layer, "triplanarHeightTransition");
                }
            }
            EditorGUILayout.Space(2f);
        }

        private static void DrawRelative(SerializedProperty parent, string name)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name));
        }

        private static TerrainSurfaceGroup FindMatchingGroup(TerrainSurfaceProfile profile)
        {
            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Profile == profile)
                {
                    return groups[i];
                }
            }
            return null;
        }

        private static void ApplyProfileToLoadedObjects(TerrainSurfaceProfile profile)
        {
            TerrainSurfaceGroup[] groups = Object.FindObjectsByType<TerrainSurfaceGroup>(
                FindObjectsInactive.Include);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Profile == profile)
                {
                    groups[i].Synchronize();
                    EditorUtility.SetDirty(groups[i]);
                }
            }

            TerrainSurfaceMeshBlend[] meshBlends = Object.FindObjectsByType<TerrainSurfaceMeshBlend>(
                FindObjectsInactive.Include);
            for (int i = 0; i < meshBlends.Length; i++)
            {
                if (meshBlends[i].TerrainGroup != null && meshBlends[i].TerrainGroup.Profile == profile)
                {
                    meshBlends[i].Synchronize();
                    EditorUtility.SetDirty(meshBlends[i]);
                }
            }
        }
    }
}
