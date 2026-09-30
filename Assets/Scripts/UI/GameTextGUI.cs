using UnityEngine;

namespace ReturnToTheEigth.UI
{
    /// <summary>Draws white game text over the dialogue-box sprite at twice its native pixel size.</summary>
    public static class GameTextGUI
    {
        private const string TypographyFontName = "TypographySilver";
        private const int BaseFontSize = 32;
        private const int MinimumFontSize = 12;
        private const float BackgroundScale = 2f;
        private const float BackgroundVerticalOffset = 40f;
        private const float HorizontalTextInsetRatio = 0.08f;
        private const float VerticalTextInsetRatio = 0.18f;
        private static readonly Color TextColor = Color.white;
        private static GUIStyle labelStyle;
        private static Font typographyFont;

        /// <summary>Draws the dialogue-box sprite at twice its native size and fits the label inside its frame.</summary>
        public static void DrawLabel(Rect rect, string text, Sprite backgroundSprite, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            EnsureResources();
            Color previousColor = GUI.color;
            Rect labelRect = rect;
            if (backgroundSprite != null)
            {
                Rect spriteRect = backgroundSprite.textureRect;
                float width = spriteRect.width * BackgroundScale;
                float height = spriteRect.height * BackgroundScale;
                Rect backgroundRect = new Rect(
                    rect.center.x - width * 0.5f,
                    rect.center.y - height * 0.5f - BackgroundVerticalOffset,
                    width,
                    height);
                Rect textureCoordinates = new Rect(
                    spriteRect.x / backgroundSprite.texture.width,
                    spriteRect.y / backgroundSprite.texture.height,
                    spriteRect.width / backgroundSprite.texture.width,
                    spriteRect.height / backgroundSprite.texture.height);

                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(backgroundRect, backgroundSprite.texture, textureCoordinates, true);

                float horizontalInset = width * HorizontalTextInsetRatio;
                float verticalInset = height * VerticalTextInsetRatio;
                labelRect = new Rect(
                    backgroundRect.x + horizontalInset,
                    backgroundRect.y + verticalInset,
                    backgroundRect.width - horizontalInset * 2f,
                    backgroundRect.height - verticalInset * 2f);
            }

            labelStyle.alignment = alignment;
            labelStyle.fontSize = GetFittingFontSize(text, labelRect.width, labelRect.height);
            GUI.color = TextColor;
            GUI.Label(labelRect, text, labelStyle);
            GUI.color = previousColor;
        }

        private static int GetFittingFontSize(string text, float width, float height)
        {
            string displayText = text ?? string.Empty;
            GUIContent displayContent = new GUIContent(displayText);
            int maximumFontSize = Mathf.RoundToInt(BaseFontSize * BackgroundScale);
            for (int fontSize = maximumFontSize; fontSize > MinimumFontSize * BackgroundScale; fontSize--)
            {
                labelStyle.fontSize = fontSize;
                if (labelStyle.CalcHeight(displayContent, width) <= height)
                {
                    return fontSize;
                }
            }

            return Mathf.RoundToInt(MinimumFontSize * BackgroundScale);
        }

        private static void EnsureResources()
        {
            if (typographyFont == null)
            {
                typographyFont = Resources.Load<Font>(TypographyFontName);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(BaseFontSize * BackgroundScale),
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