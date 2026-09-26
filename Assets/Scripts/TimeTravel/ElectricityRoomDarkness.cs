using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Darkens the upper Hall room after the piano is solved until the electricity puzzle restores power.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityRoomDarkness : MonoBehaviour
    {
        private const string PianoPuzzleId = "PianoPuzzle";
        private const string ElectricityPuzzleId = "ElectricityPuzzle";
        private const string DarknessSortingLayer = "Assets and player";
        private const float DarknessAlpha = 0.78f;
        private const float OnePixel = 1f;
        private const int DarknessSortingOrder = 19;
        private static readonly Color DarknessColor = new Color(0.015f, 0.02f, 0.07f, DarknessAlpha);
        private static readonly Rect OnePixelRect = new Rect(0f, 0f, OnePixel, OnePixel);
        private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

        private SpriteRenderer darknessRenderer;
        private Texture2D darknessTexture;
        private Sprite darknessSprite;

        private void Awake()
        {
            darknessRenderer = GetComponent<SpriteRenderer>();
            if (darknessRenderer == null)
            {
                darknessRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            darknessTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "ElectricityRoomDarknessTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            darknessTexture.SetPixel(0, 0, Color.white);
            darknessTexture.Apply();
            darknessSprite = Sprite.Create(darknessTexture, OnePixelRect, CenterPivot, OnePixel);
            darknessSprite.name = "ElectricityRoomDarknessSprite";

            darknessRenderer.sprite = darknessSprite;
            darknessRenderer.color = DarknessColor;
            darknessRenderer.sortingLayerName = DarknessSortingLayer;
            darknessRenderer.sortingOrder = DarknessSortingOrder;
            UpdateDarknessState();
        }

        private void Update()
        {
            UpdateDarknessState();
        }

        private void OnDestroy()
        {
            if (darknessSprite != null)
            {
                Destroy(darknessSprite);
            }
            if (darknessTexture != null)
            {
                Destroy(darknessTexture);
            }
        }

        private void UpdateDarknessState()
        {
            GameManager gameManager = GameManager.Instance;
            bool hasCompletedPiano = gameManager != null && gameManager.IsPuzzleCompleted(PianoPuzzleId);
            bool hasCompletedElectricity = gameManager != null && gameManager.IsPuzzleCompleted(ElectricityPuzzleId);
            darknessRenderer.enabled = hasCompletedPiano && !hasCompletedElectricity;
        }
    }
}
