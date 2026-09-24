using ReturnToTheEigth.Events;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Player
{
    /// <summary>Temporary pixel character, replaced by authored era sprites when those are supplied separately.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelPlayerVisual : MonoBehaviour
    {
        private const string PresentTextureName = "Placeholder_Present32Style";
        private const string PastTextureName = "Placeholder_Past8Style";
        private const int FirstIndex = 0;
        private const int LastIndexOffset = 1;
        private const int PresentWidth = 12;
        private const int PresentHeight = 20;
        private const int PastWidth = 6;
        private const int PastHeight = 10;
        private const int NoExtrusion = 0;
        private const float PresentPixelsPerUnit = 50f;
        private const float PastPixelsPerUnit = 25f;
        private const float PivotX = 0.5f;
        private const float PivotY = 0.1f;
        private const float Zero = 0f;
        private const int SortingOrder = 20;
        private static readonly Color Outline = new Color(0.08f, 0.12f, 0.18f, 1f);
        private static readonly Color Skin = new Color(0.95f, 0.72f, 0.46f, 1f);
        private static readonly Color Shirt = new Color(0.05f, 0.68f, 0.88f, 1f);
        private static readonly Color Highlight = new Color(0.47f, 0.92f, 1f, 1f);
        private static readonly string[] PresentPixels =
        {
            "....OOOO....", "...OOOOOO...", "..OOOOOOOO..", "..OOSSSSOO..",
            "..OSSSSSSO..", "..OSOSOSSO..", "...SSSSSS...", "....SSSS....",
            "..OOHHHHOO..", ".OOHHHTTTOO.", ".OSTHHTTTSO.", ".OSTTTTTTSO.",
            ".OSTTTTTTSO.", "..OTTTTTTO..", "...OOOOOO...", "...OOOOOO...",
            "...OO..OO...", "...OO..OO...", "..OOO..OOO..", "..OOO..OOO.."
        };
        private static readonly string[] PastPixels =
        {
            "..OO..", ".OOOO.", ".OSSO.", ".SSSS.", "..SS..",
            ".OTTO.", "OSTTSO", ".OTTO.", "..OO..", ".OOOO."
        };
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private Sprite presentSprite;
        [SerializeField] private Sprite pastSprite;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Sprite generatedPresentSprite;
        private Sprite generatedPastSprite;
        private Texture2D presentTexture;
        private Texture2D pastTexture;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            spriteRenderer.sortingOrder = SortingOrder;
            if (presentSprite == null)
                generatedPresentSprite = BuildSprite(PresentPixels, PresentWidth, PresentHeight,
                    PresentPixelsPerUnit, PresentTextureName, out presentTexture);
            if (pastSprite == null)
                generatedPastSprite = BuildSprite(PastPixels, PastWidth, PastHeight,
                    PastPixelsPerUnit, PastTextureName, out pastTexture);
            ApplyEra(TimelineEra.Present);
        }

        private void OnEnable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged += ApplyEra;
        }

        private void OnDisable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged -= ApplyEra;
        }

        private void OnDestroy()
        {
            if (generatedPresentSprite != null) Destroy(generatedPresentSprite);
            if (generatedPastSprite != null) Destroy(generatedPastSprite);
            if (presentTexture != null) Destroy(presentTexture);
            if (pastTexture != null) Destroy(pastTexture);
        }

        private void ApplyEra(TimelineEra era)
        {
            if (era == TimelineEra.Present && animator != null && animator.enabled
                && animator.runtimeAnimatorController != null)
                return;

            spriteRenderer.sprite = era == TimelineEra.Present
                ? presentSprite != null ? presentSprite : generatedPresentSprite
                : pastSprite != null ? pastSprite : generatedPastSprite;
        }

        private static Sprite BuildSprite(string[] rows, int width, int height, float pixelsPerUnit,
            string textureName, out Texture2D texture)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = textureName, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[width * height];
            for (int y = FirstIndex; y < height; y++)
                for (int x = FirstIndex; x < width; x++)
                {
                    char code = rows[height - y - LastIndexOffset][x];
                    pixels[y * width + x] = code == 'O' ? Outline : code == 'S' ? Skin
                        : code == 'T' ? Shirt : code == 'H' ? Highlight : Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(Zero, Zero, width, height),
                new Vector2(PivotX, PivotY), pixelsPerUnit, NoExtrusion, SpriteMeshType.FullRect);
        }
    }
}
