using UnityEngine;

namespace ReturnToTheEigth.UI
{
    /// <summary>Draws white game text over a dialogue-box sprite, scaling the layout to the active safe area.</summary>
    public static class GameTextGUI
    {
        private const string TypographyFontName = "TypographySilver";
        private const int BaseFontSize = 32;
        private const int MinimumFontSize = 14;
        private const int MaximumFontSize = 32;
        private const float ReferenceScreenWidth = 1920f;
        private const float ReferenceScreenHeight = 1080f;
        private const float DialogueWidthRatio = 0.75f;
        private const float DialogueHeightRatio = 0.28f;
        private const float DialogueBottomInsetRatio = 0.025f;
        private const float HorizontalTextInsetRatio = 0.08f;
        private const float VerticalTextInsetRatio = 0.18f;
        private const float DialogueBackgroundScale = 0.65f;
        private const float CharacterPictureHeightRatio = 0.82f;
        private const float CharacterPictureLeftInsetRatio = 0.04f;
        private const float CharacterPictureTextGapRatio = 0.08f;
        private const float DialogueRightInsetRatio = 0.08f;
        private const float MinimumResolutionScale = 0.7f;
        private const float MaximumResolutionScale = 1.6f;
        private const float MaximumDialogueBackgroundHeightRatio = 0.32f;
        private static readonly Color TextColor = Color.white;
        private static GUIStyle labelStyle;
        private static Font typographyFont;

        /// <summary>Gets the safe on-screen area shared by dialogue and cinematic text.</summary>
        public static Rect GetSafeAreaRect()
        {
            Rect safeArea = Screen.safeArea;
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.targetDisplay == 0)
            {
                Rect cameraPixelRect = mainCamera.pixelRect;
                safeArea = IntersectRects(safeArea, cameraPixelRect);
            }

            float guiY = Screen.height - safeArea.yMax;
            return new Rect(safeArea.xMin, guiY, safeArea.width, safeArea.height);
        }

        /// <summary>Creates the shared dialogue panel, centered horizontally and anchored to the bottom of the safe area.</summary>
        public static Rect GetStandardDialogueRect()
        {
            Rect safeArea = GetSafeAreaRect();
            float panelWidth = safeArea.width * DialogueWidthRatio;
            float panelHeight = safeArea.height * DialogueHeightRatio;
            float bottomInset = safeArea.height * DialogueBottomInsetRatio;
            return new Rect(safeArea.center.x - panelWidth * 0.5f,
                safeArea.yMax - bottomInset - panelHeight,
                panelWidth,
                panelHeight);
        }

        /// <summary>Gets the shared, resolution-scaled text size used by in-game dialogue and item labels.</summary>
        public static int GetStandardFontSize()
        {
            Rect safeArea = GetSafeAreaRect();
            float resolutionScale = GetResolutionScale(safeArea);
            int scaledFontSize = Mathf.RoundToInt(BaseFontSize * resolutionScale);
            return Mathf.Clamp(scaledFontSize, MinimumFontSize, MaximumFontSize);
        }

        /// <summary>Draws a standard-size wrapped game label with the shared pixel font.</summary>
        public static void DrawText(Rect rect, string text, TextAnchor alignment, Color color)
        {
            EnsureResources();
            Color previousColor = GUI.color;
            labelStyle.alignment = alignment;
            labelStyle.fontSize = GetStandardFontSize();
            SetStyleTextColor(color);
            GUI.color = color;
            GUI.Label(rect, text, labelStyle);
            GUI.color = previousColor;
        }

        /// <summary>Draws a character portrait and dialogue-box sprite as one bottom-centered group with wrapped text.</summary>
        public static void DrawLabel(Rect rect, string text, Sprite backgroundSprite,
            TextAnchor alignment = TextAnchor.MiddleCenter, Sprite characterPicture = null)
        {
            EnsureResources();
            Rect safeArea = GetSafeAreaRect();
            rect = FitRectInside(rect, safeArea);
            Rect labelRect = rect;
            Color previousColor = GUI.color;
            if (backgroundSprite != null)
            {
                Rect spriteRect = backgroundSprite.textureRect;
                if (spriteRect.width > 0f && spriteRect.height > 0f)
                {
                    float backgroundWidth = rect.width * DialogueBackgroundScale;
                    float backgroundHeight = backgroundWidth * spriteRect.height / spriteRect.width;
                    float maximumHeight = safeArea.height * MaximumDialogueBackgroundHeightRatio;
                    if (backgroundHeight > maximumHeight)
                    {
                        float scale = maximumHeight / backgroundHeight;
                        backgroundWidth *= scale;
                        backgroundHeight *= scale;
                    }

                    Rect backgroundRect = new Rect(
                        rect.center.x - backgroundWidth * 0.5f,
                        rect.yMax - backgroundHeight,
                        backgroundWidth,
                        backgroundHeight);
                    DrawSprite(backgroundSprite, backgroundRect);

                    float horizontalInset = backgroundWidth * HorizontalTextInsetRatio;
                    Rect characterSpriteRect = characterPicture != null ? characterPicture.textureRect : Rect.zero;

                    float verticalInset = backgroundHeight * VerticalTextInsetRatio;
                    float textLeft = backgroundRect.x + horizontalInset;
                    float textRight = backgroundRect.xMax - horizontalInset;
                    if (characterPicture != null && characterSpriteRect.height > 0f)
                    {
                        float characterHeight = backgroundHeight * CharacterPictureHeightRatio;
                        float characterWidth = characterHeight * characterSpriteRect.width / characterSpriteRect.height;
                        float characterLeft = backgroundRect.x + backgroundWidth * CharacterPictureLeftInsetRatio;
                        float characterTop = backgroundRect.y + (backgroundHeight - characterHeight) * 0.5f;
                        Rect characterRect = new Rect(characterLeft, characterTop, characterWidth, characterHeight);
                        DrawSprite(characterPicture, characterRect);
                        textLeft = characterRect.xMax + backgroundHeight * CharacterPictureTextGapRatio;
                        labelRect = new Rect(textLeft, backgroundRect.y + verticalInset,
                            Mathf.Max(textRight - textLeft, 0f), backgroundHeight - verticalInset * 2f);
                        alignment = TextAnchor.MiddleCenter;
                    }
                    else
                    {
                        labelRect = new Rect(
                            backgroundRect.x + horizontalInset,
                            backgroundRect.y + verticalInset,
                            backgroundWidth - horizontalInset * 2f,
                            backgroundHeight - verticalInset * 2f);
                    }
                }
            }

            labelStyle.alignment = alignment;
            labelStyle.fontSize = GetStandardFontSize();
            FitFontSizeToRect(text, labelRect);
            SetStyleTextColor(TextColor);
            GUI.color = TextColor;
            GUI.Label(labelRect, text, labelStyle);
            GUI.color = previousColor;
        }

        private static void DrawSprite(Sprite sprite, Rect destination)
        {
            Rect spriteRect = sprite.textureRect;
            Rect textureCoordinates = new Rect(
                spriteRect.x / sprite.texture.width,
                spriteRect.y / sprite.texture.height,
                spriteRect.width / sprite.texture.width,
                spriteRect.height / sprite.texture.height);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(destination, sprite.texture, textureCoordinates, true);
        }

        private static void FitFontSizeToRect(string text, Rect rect)
        {
            GUIContent content = new GUIContent(text ?? string.Empty);
            while (labelStyle.fontSize > MinimumFontSize
                   && labelStyle.CalcHeight(content, rect.width) > rect.height)
            {
                labelStyle.fontSize--;
            }
        }


        private static Rect IntersectRects(Rect first, Rect second)
        {
            float minimumX = Mathf.Max(first.xMin, second.xMin);
            float minimumY = Mathf.Max(first.yMin, second.yMin);
            float maximumX = Mathf.Min(first.xMax, second.xMax);
            float maximumY = Mathf.Min(first.yMax, second.yMax);
            return new Rect(minimumX, minimumY,
                Mathf.Max(maximumX - minimumX, 0f),
                Mathf.Max(maximumY - minimumY, 0f));
        }

        private static Rect FitRectInside(Rect rect, Rect bounds)
        {
            rect.width = Mathf.Min(rect.width, bounds.width);
            rect.height = Mathf.Min(rect.height, bounds.height);
            rect.x = Mathf.Clamp(rect.x, bounds.xMin, bounds.xMax - rect.width);
            rect.y = Mathf.Clamp(rect.y, bounds.yMin, bounds.yMax - rect.height);
            return rect;
        }

        private static float GetResolutionScale(Rect safeArea)
        {
            float scale = Mathf.Min(safeArea.width / ReferenceScreenWidth,
                safeArea.height / ReferenceScreenHeight);
            return Mathf.Clamp(scale, MinimumResolutionScale, MaximumResolutionScale);
        }

        private static void SetStyleTextColor(Color color)
        {
            labelStyle.normal.textColor = color;
            labelStyle.hover.textColor = color;
            labelStyle.active.textColor = color;
            labelStyle.focused.textColor = color;
            labelStyle.onNormal.textColor = color;
            labelStyle.onHover.textColor = color;
            labelStyle.onActive.textColor = color;
            labelStyle.onFocused.textColor = color;
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
                    fontSize = BaseFontSize,
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