using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    [CustomEditor(typeof(TerrainSurfaceGroup))]
    internal sealed class TerrainSurfaceGroupEditor : UnityEditor.Editor
    {
        private string operationMessage;
        private MessageType operationMessageType;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            TerrainSurfaceGroup group = (TerrainSurfaceGroup)target;
            EditorGUILayout.Space();
            DrawStatus(group);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Profile + Material"))
                {
                    CreateSetupAssets(group);
                }

                if (GUILayout.Button("Synchronize Layers"))
                {
                    RunOperation(TerrainSurfaceTextureArrayBuilder.SynchronizeProfileLayers(group, out operationMessage));
                }
            }

            using (new EditorGUI.DisabledScope(group.Profile == null))
            {
                if (GUILayout.Button("Build / Rebuild Texture Arrays"))
                {
                    RunOperation(TerrainSurfaceTextureArrayBuilder.Build(group, out operationMessage));
                }
            }

            if (GUILayout.Button("Apply Profile To Terrain Group"))
            {
                Undo.RecordObject(group, "Apply Terrain Surface Profile");
                group.Synchronize();
                EditorUtility.SetDirty(group);
                operationMessage = group.ValidationMessage;
                operationMessageType = GetMessageType(operationMessage);
            }

            if (!string.IsNullOrWhiteSpace(operationMessage))
            {
                EditorGUILayout.HelpBox(operationMessage, operationMessageType);
            }
        }

        private static void DrawStatus(TerrainSurfaceGroup group)
        {
            string status = group.ValidationMessage;
            if (!string.IsNullOrWhiteSpace(status))
            {
                EditorGUILayout.HelpBox(status, GetMessageType(status));
            }

            if (group.Profile != null && GUILayout.Button("Select Profile For Detailed Configuration"))
            {
                Selection.activeObject = group.Profile;
                EditorGUIUtility.PingObject(group.Profile);
            }
        }

        private void CreateSetupAssets(TerrainSurfaceGroup group)
        {
            Shader shader = TerrainToolsAssetLocator.FindTerrainShader();
            if (shader == null)
            {
                operationMessage = "Terrain Surface shader is still importing or could not be found.";
                operationMessageType = MessageType.Error;
                return;
            }

            const string generatedFolder = TerrainToolsPaths.TerrainSurfaceGeneratedRoot;
            TerrainToolsPaths.EnsureAssetFolder(generatedFolder);
            string safeName = string.IsNullOrWhiteSpace(group.name) ? "Terrain" : group.name;
            string profilePath = AssetDatabase.GenerateUniqueAssetPath($"{generatedFolder}/{safeName}_SurfaceProfile.asset");
            string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{generatedFolder}/{safeName}_SurfaceMaterial.mat");

            TerrainSurfaceProfile profile = CreateInstance<TerrainSurfaceProfile>();
            TerrainToolsAssetLocator.AssignDefaultProfileAssets(profile);
            Material material = new Material(shader);
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(group, "Create Terrain Surface Setup");
            group.SetGeneratedSetup(profile, material);
            EditorUtility.SetDirty(group);
            Selection.activeObject = profile;
            operationMessage = "Created the profile and terrain material. Synchronize layers, configure the profile, then build arrays.";
            operationMessageType = MessageType.Info;
        }

        private void RunOperation(bool succeeded)
        {
            operationMessageType = succeeded ? MessageType.Info : MessageType.Error;
            Repaint();
        }

        private static MessageType GetMessageType(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return MessageType.None;
            }

            if (message.StartsWith("Ready") || message.StartsWith("Built") || message.StartsWith("Synchronized"))
            {
                return MessageType.Info;
            }

            return MessageType.Warning;
        }

    }
}
