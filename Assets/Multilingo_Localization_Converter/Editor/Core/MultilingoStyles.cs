using UnityEngine;
using UnityEditor;

namespace Multilingo.Localization.Editor
{
    /// <summary>
    /// Shared GUI styles and utility methods used across all Multilingo editor windows.
    /// Eliminates duplicated MakeTex(), style definitions, and color constants.
    /// </summary>
    public static class MultilingoStyles
    {
        // =====================
        // Color Palette
        // =====================
        public static readonly Color AccentPurple = new Color(0.6f, 0.5f, 1f);
        public static readonly Color AccentGreen = new Color(0.4f, 0.9f, 0.6f);
        public static readonly Color AccentBlue = new Color(0.4f, 0.7f, 1f);
        public static readonly Color AccentOrange = new Color(0.9f, 0.6f, 0.4f);
        public static readonly Color AccentRed = new Color(1f, 0.4f, 0.4f);
        public static readonly Color AccentPink = new Color(0.8f, 0.7f, 1f);
        public static readonly Color BgDark = new Color(0.15f, 0.15f, 0.18f);
        public static readonly Color BgMedium = new Color(0.22f, 0.22f, 0.25f);
        public static readonly Color TextLight = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color TextDim = new Color(0.7f, 0.7f, 0.7f);

        // Button colors
        public static readonly Color BtnPrimary = new Color(0.3f, 0.5f, 0.9f);
        public static readonly Color BtnSuccess = new Color(0.2f, 0.7f, 0.4f);
        public static readonly Color BtnWarning = new Color(0.8f, 0.4f, 0.2f);
        public static readonly Color BtnDanger = new Color(0.7f, 0.2f, 0.2f);

        // =====================
        // Texture Utilities
        // =====================

        /// <summary>
        /// Create a solid-color Texture2D. Centralized version of the MakeTex() method
        /// that was previously duplicated in 4 separate files.
        /// </summary>
        public static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        // =====================
        // Style Factories
        // =====================

        /// <summary>
        /// Create a styled button (colored background, bold text).
        /// </summary>
        public static GUIStyle MakeButton(Color bgColor, int fontSize = 14, int height = 40)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                fixedHeight = height
            };
            style.normal.textColor = Color.white;
            style.normal.background = MakeTex(2, 2, bgColor);
            style.hover.background = MakeTex(2, 2, Lighten(bgColor, 0.15f));
            return style;
        }

        /// <summary>
        /// Create a section header style.
        /// </summary>
        public static GUIStyle MakeSectionHeader(Color color, int fontSize = 16)
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = fontSize,
                normal = { textColor = color }
            };
        }

        /// <summary>
        /// Create a tab header style.
        /// </summary>
        public static GUIStyle MakeTabHeader(Color color)
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color },
                margin = new RectOffset(0, 0, 10, 5)
            };
        }

        /// <summary>
        /// Create a progress bar style.
        /// </summary>
        public static void DrawProgressBar(float progress, string label, Color barColor)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            Rect filled = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
            EditorGUI.DrawRect(filled, barColor);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };
            GUI.Label(rect, label, labelStyle);
        }

        /// <summary>
        /// Draw a separator line.
        /// </summary>
        public static void DrawSeparator(float height = 1)
        {
            GUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
            GUILayout.Space(5);
        }

        // =====================
        // Helpers
        // =====================

        static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }
    }
}
