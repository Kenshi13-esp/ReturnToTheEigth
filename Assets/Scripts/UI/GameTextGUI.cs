using UnityEngine;

namespace ReturnToTheEigth.UI
{
    /// <summary>Draws consistent 32-pixel white TypographySilver text over a black rectangle for the game's IMGUI screens.</summary>
    public static class GameTextGUI
    {
        private const string TypographyFontName = "TypographySilver";
        private const float BackgroundAlpha = 0.92f;
        private const int FontSize = 32;
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, BackgroundAlpha);
        private static readonly Color TextColor = Color.white;
        private static GUIStyle labelStyle;
        private static Texture2D rectangleTexture;
        private static Font typographyFont;

        /// <summary>Draws a black-backed, white TypographySilver label at the standard game font size.</summary>
        public static void DrawLabel(Rect rect, string text, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            EnsureResources();
            Color previousColor = GUI.color;
            GUI.color = BackgroundColor;
            GUI.DrawTexture(rect, rectangleTexture, ScaleMode.StretchToFill, false);
            GUI.color = previousColor;

            labelStyle.alignment = alignment;
            GUI.Label(rect, text, labelStyle);
        }

        private static void EnsureResources()
        {
            if (rectangleTexture == null)
            {
                rectangleTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                rectangleTexture.SetPixel(0, 0, Color.white);
                rectangleTexture.Apply();
            }

            if (typographyFont == null)
            {
                typographyFont = Resources.Load<Font>(TypographyFontName);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = FontSize,
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    normal = { textColor = TextColor },
                    hover = { textColor = TextColor },
                    active = { textColor = TextColor },
                    focused = { textColor = TextColor },
                    onNormal = { textColor = TextColor },
                    onHover = { textColor = TextColor },
                    onActive = { textColor = TextColor },
                    onFocused = { textColor = TextColor }
                };
            }

            labelStyle.font = typographyFont;
            labelStyle.fontSize = FontSize;
            labelStyle.normal.textColor = TextColor;
            labelStyle.hover.textColor = TextColor;
            labelStyle.active.textColor = TextColor;
            labelStyle.focused.textColor = TextColor;
            labelStyle.onNormal.textColor = TextColor;
            labelStyle.onHover.textColor = TextColor;
            labelStyle.onActive.textColor = TextColor;
            labelStyle.onFocused.textColor = TextColor;
        }
    }
}
