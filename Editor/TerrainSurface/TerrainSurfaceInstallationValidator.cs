using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainSurfaceInstallationValidator
    {
        private readonly struct ShaderDefinition
        {
            internal readonly string Path;
            internal readonly string Name;

            internal ShaderDefinition(string path, string name)
            {
                Path = path;
                Name = name;
            }
        }

        private static readonly ShaderDefinition[] Shaders =
        {
            new ShaderDefinition(
                TerrainToolsAssetLocator.TerrainShaderPath,
                TerrainToolsAssetLocator.TerrainShaderName),
            new ShaderDefinition(
                TerrainToolsAssetLocator.MeshBlendShaderPath,
                TerrainToolsAssetLocator.MeshBlendShaderName),
            new ShaderDefinition(
                TerrainToolsAssetLocator.PackingShaderPath,
                TerrainToolsAssetLocator.PackingShaderName)
        };

        [MenuItem("Tools/Terrain Tools/Terrain Surface/Validate Installation")]
        private static void ValidateInstallation()
        {
            StringBuilder report = new StringBuilder();
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < Shaders.Length; i++)
            {
                ShaderDefinition definition = Shaders[i];
                AssetDatabase.ImportAsset(definition.Path, ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(definition.Path);
                if (shader == null)
                {
                    report.AppendLine($"ERROR: Could not import {definition.Path}");
                    errors++;
                    continue;
                }
                if (shader.name != definition.Name)
                {
                    report.AppendLine(
                        $"ERROR: Expected shader name '{definition.Name}', imported '{shader.name}'.");
                    errors++;
                }
                if (!shader.isSupported)
                {
                    report.AppendLine($"ERROR: Shader '{definition.Name}' is not supported on the active graphics API.");
                    errors++;
                }

                UnityEditor.ShaderMessage[] messages =
    UnityEditor.ShaderUtil.GetShaderMessages(shader);
                for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++)
                {
                    ShaderMessage message = messages[messageIndex];
                    if (message.severity == ShaderCompilerMessageSeverity.Error)
                    {
                        errors++;
                        report.AppendLine(
                            $"ERROR {definition.Name} ({message.platform}) line {message.line}: {message.message}");
                    }
                    else
                    {
                        warnings++;
                        report.AppendLine(
                            $"WARNING {definition.Name} ({message.platform}) line {message.line}: {message.message}");
                    }
                }
            }

            if (errors == 0 && warnings == 0)
            {
                report.AppendLine("All Terrain Surface shaders imported without compiler messages.");
            }
            else
            {
                report.Insert(0, $"Terrain Surface validation: {errors} error(s), {warnings} warning(s).\n");
            }

            string text = report.ToString();
            if (errors > 0)
            {
                Debug.LogError(text);
            }
            else if (warnings > 0)
            {
                Debug.LogWarning(text);
            }
            else
            {
                Debug.Log(text);
            }

            EditorUtility.DisplayDialog(
                errors == 0 ? "Terrain Surface Validation" : "Terrain Surface Validation Failed",
                text,
                "OK");
        }
    }
}
