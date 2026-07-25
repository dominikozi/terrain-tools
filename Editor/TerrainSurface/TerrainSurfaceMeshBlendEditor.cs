using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [CustomEditor(typeof(TerrainSurfaceMeshBlend))]
    internal sealed class TerrainSurfaceMeshBlendEditor : UnityEditor.Editor
    {
        private SerializedProperty blendMaterial;

        private void OnEnable()
        {
            blendMaterial = serializedObject.FindProperty("blendMaterial");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            TerrainSurfaceMeshBlend blend = (TerrainSurfaceMeshBlend)target;
            if (!string.IsNullOrWhiteSpace(blend.ValidationMessage))
            {
                MessageType type = blend.ValidationMessage.StartsWith("Ready")
                    ? MessageType.Info
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(blend.ValidationMessage, type);
            }

            if (blendMaterial.objectReferenceValue == null && GUILayout.Button("Create Mesh Blend Material"))
            {
                Shader shader = TerrainToolsAssetLocator.FindMeshBlendShader();
                if (shader == null)
                {
                    EditorUtility.DisplayDialog("Terrain Surface", "Mesh Blend shader was not found.", "OK");
                }
                else
                {
                    const string folder = TerrainToolsPaths.TerrainSurfaceGeneratedRoot;
                    TerrainToolsPaths.EnsureAssetFolder(folder);
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{folder}/{blend.name}_TerrainBlend.mat");
                    Material material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                    serializedObject.Update();
                    blendMaterial.objectReferenceValue = material;
                    serializedObject.ApplyModifiedProperties();
                    blend.Synchronize();
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button("Synchronize Terrain Tiles"))
            {
                Undo.RecordObject(blend, "Synchronize Terrain Mesh Blend");
                blend.Synchronize();
                EditorUtility.SetDirty(blend);
            }
        }
    }
}
