using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor.Painters
{

internal readonly struct TerrainPrototypePickerOption
{
    public TerrainPrototypePickerOption(
        int index,
        string name,
        string subtitle,
        UnityEngine.Object previewSource,
        Texture previewTexture,
        bool enabled,
        string disabledReason = null)
    {
        Index = index;
        Name = string.IsNullOrWhiteSpace(name) ? $"Prototype {index}" : name;
        Subtitle = subtitle ?? string.Empty;
        PreviewSource = previewSource;
        PreviewTexture = previewTexture;
        Enabled = enabled;
        DisabledReason = disabledReason ?? string.Empty;
    }

    public int Index { get; }

    public string Name { get; }

    public string Subtitle { get; }

    public UnityEngine.Object PreviewSource { get; }

    public Texture PreviewTexture { get; }

    public bool Enabled { get; }

    public string DisabledReason { get; }

    public string Tooltip => string.IsNullOrEmpty(DisabledReason)
        ? $"{Name}\n{Subtitle}"
        : $"{Name}\n{Subtitle}\n{DisabledReason}";
}

internal static class TerrainPrototypePicker
{
    public const float SelectorHeight = 42f;

    private const float SelectorPreviewSize = 36f;

    public static bool DrawSelector(
        Rect rect,
        GUIContent label,
        string displayName,
        UnityEngine.Object previewSource,
        Texture previewTexture,
        bool hasSelection,
        string tooltip = null)
    {
        Rect valueRect = EditorGUI.PrefixLabel(rect, label);
        GUIContent buttonContent = new(string.Empty, tooltip);
        bool clicked = GUI.Button(valueRect, buttonContent, EditorStyles.objectField);

        Rect previewRect = new(
            valueRect.x + 3f,
            valueRect.y + 3f,
            SelectorPreviewSize,
            Mathf.Max(1f, valueRect.height - 6f));
        Texture preview = ResolvePreview(previewSource, previewTexture);
        if (preview != null)
        {
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUI.Label(previewRect, "?", EditorStyles.centeredGreyMiniLabel);
        }

        Rect textRect = new(
            previewRect.xMax + 6f,
            valueRect.y,
            Mathf.Max(1f, valueRect.width - SelectorPreviewSize - 28f),
            valueRect.height);
        GUIStyle textStyle = new(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            fontStyle = hasSelection ? FontStyle.Normal : FontStyle.Italic
        };
        GUI.Label(textRect, hasSelection ? displayName : "Choose from Terrain...", textStyle);

        Rect arrowRect = new(valueRect.xMax - 20f, valueRect.y, 16f, valueRect.height);
        GUI.Label(arrowRect, "\u25BE", EditorStyles.centeredGreyMiniLabel);
        return clicked;
    }

    public static void Show(
        Rect activatorRect,
        string title,
        string terrainName,
        IReadOnlyList<TerrainPrototypePickerOption> options,
        int selectedIndex,
        Action<int> onSelected)
    {
        PopupWindow.Show(
            activatorRect,
            new TerrainPrototypePickerPopup(title, terrainName, options, selectedIndex, onSelected));
    }

    public static Rect GetCurrentEventAnchor()
    {
        Vector2 mousePosition = Event.current != null ? Event.current.mousePosition : Vector2.zero;
        return new Rect(mousePosition, Vector2.one);
    }

    private static Texture ResolvePreview(UnityEngine.Object source, Texture explicitTexture)
    {
        if (explicitTexture != null)
        {
            return explicitTexture;
        }

        if (source == null)
        {
            return null;
        }

        return AssetPreview.GetAssetPreview(source) ?? AssetPreview.GetMiniThumbnail(source);
    }

    private sealed class TerrainPrototypePickerPopup : PopupWindowContent
    {
        private const float WindowWidth = 430f;
        private const float WindowHeight = 390f;
        private const float TileWidth = 92f;
        private const float TileHeight = 108f;
        private const float TileSpacing = 6f;

        private readonly string title;
        private readonly string terrainName;
        private readonly List<TerrainPrototypePickerOption> options;
        private readonly int selectedIndex;
        private readonly Action<int> onSelected;

        private Vector2 scrollPosition;
        private string search = string.Empty;

        public TerrainPrototypePickerPopup(
            string title,
            string terrainName,
            IReadOnlyList<TerrainPrototypePickerOption> options,
            int selectedIndex,
            Action<int> onSelected)
        {
            this.title = title;
            this.terrainName = terrainName;
            this.selectedIndex = selectedIndex;
            this.onSelected = onSelected;
            this.options = options != null
                ? new List<TerrainPrototypePickerOption>(options)
                : new List<TerrainPrototypePickerOption>();
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(WindowWidth, WindowHeight);
        }

        public override void OnGUI(Rect rect)
        {
            HandleKeyboard();

            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(terrainName) ? "No Terrain selected" : terrainName,
                EditorStyles.miniLabel);

            GUILayout.Space(3f);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            GUILayout.Space(3f);

            List<int> visibleIndices = GetVisibleIndices();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawGrid(visibleIndices);
            EditorGUILayout.EndScrollView();

            string summary = options.Count == 0
                ? "The selected Terrain has no compatible prototypes."
                : $"{visibleIndices.Count} of {options.Count} Terrain prototypes";
            EditorGUILayout.LabelField(summary, EditorStyles.centeredGreyMiniLabel);

            if (AssetPreview.IsLoadingAssetPreviews())
            {
                editorWindow.Repaint();
            }
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != KeyCode.Escape)
            {
                return;
            }

            editorWindow.Close();
            current.Use();
        }

        private List<int> GetVisibleIndices()
        {
            List<int> visible = new(options.Count);
            string trimmedSearch = search?.Trim();
            for (int i = 0; i < options.Count; i++)
            {
                TerrainPrototypePickerOption option = options[i];
                if (!string.IsNullOrEmpty(trimmedSearch)
                    && option.Name.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) < 0
                    && option.Subtitle.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                visible.Add(i);
            }

            return visible;
        }

        private void DrawGrid(IReadOnlyList<int> visibleIndices)
        {
            int columns = Mathf.Max(
                1,
                Mathf.FloorToInt((WindowWidth - 18f + TileSpacing) / (TileWidth + TileSpacing)));

            for (int start = 0; start < visibleIndices.Count; start += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int visibleIndex = start + column;
                    if (visibleIndex >= visibleIndices.Count)
                    {
                        GUILayout.Space(TileWidth + TileSpacing);
                        continue;
                    }

                    DrawTile(options[visibleIndices[visibleIndex]]);
                    GUILayout.Space(TileSpacing);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(TileSpacing);
            }
        }

        private void DrawTile(TerrainPrototypePickerOption option)
        {
            Rect tileRect = GUILayoutUtility.GetRect(
                TileWidth,
                TileHeight,
                GUILayout.Width(TileWidth),
                GUILayout.Height(TileHeight));

            if (option.Index == selectedIndex)
            {
                EditorGUI.DrawRect(tileRect, new Color(0.20f, 0.52f, 0.85f, 0.9f));
                tileRect = new Rect(tileRect.x + 2f, tileRect.y + 2f, tileRect.width - 4f, tileRect.height - 4f);
            }

            using (new EditorGUI.DisabledScope(!option.Enabled))
            {
                if (GUI.Button(tileRect, new GUIContent(string.Empty, option.Tooltip), EditorStyles.helpBox))
                {
                    onSelected?.Invoke(option.Index);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }

                Rect previewRect = new(tileRect.x + 6f, tileRect.y + 5f, tileRect.width - 12f, 62f);
                Texture preview = ResolvePreview(option.PreviewSource, option.PreviewTexture);
                if (preview != null)
                {
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.Label(previewRect, "No Preview", EditorStyles.centeredGreyMiniLabel);
                }

                GUIStyle nameStyle = new(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    clipping = TextClipping.Clip,
                    wordWrap = true
                };
                Rect nameRect = new(tileRect.x + 4f, previewRect.yMax + 2f, tileRect.width - 8f, 26f);
                GUI.Label(nameRect, option.Name, nameStyle);

                Rect subtitleRect = new(tileRect.x + 4f, tileRect.yMax - 16f, tileRect.width - 8f, 13f);
                GUI.Label(subtitleRect, option.Subtitle, EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}

}
