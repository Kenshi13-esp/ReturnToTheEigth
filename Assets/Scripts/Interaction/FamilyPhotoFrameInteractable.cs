using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.UI;
using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Opens the family photo frame and completes the game once all four hidden fragments have been collected.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class FamilyPhotoFrameInteractable : InteractableBase
    {
        private const string OpenPrompt = "Ver el marco familiar";
        private const string ClosePrompt = "Cerrar el marco";
        private const string CompletePrompt = "Cerrar el marco";
        private const string NarrativeText = "A este marco le falta la foto de mi familia.";
        private const int PanelMargin = 24;
        private const float HighlightRadius = 0.9f;
        private const float ColliderPadding = 0.4f;
        private const float ImageWidthRatio = 0.54f;
        private const float ImageHeightRatio = 0.66f;
        private const float ImageTextGap = 24f;
        private const float TextHeight = 76f;
        private const float Zero = 0f;
        private static readonly string[] FragmentIds =
        {
            PuzzleItemIds.ColorTrackPuzzlePaintingFragment,
            PuzzleItemIds.ChessPuzzlePaintingFragment,
            PuzzleItemIds.PianoPuzzlePaintingFragment,
            PuzzleItemIds.ElectricityPuzzlePaintingFragment
        };
        private static readonly Color OverlayColor = new Color(0.025f, 0.02f, 0.035f, 0.88f);

        private BoxCollider2D interactionCollider;
        private SpriteRenderer frameRenderer;
        private TopDownCharacterController playerController;
        private TimelineUnlockSequence timelineUnlockSequence;
        private Texture2D solidTexture;
        private bool isOpen;
        private bool isEnding;
        private bool isTimelineIntroPlaying;

        /// <summary>Gets whether this view should hide the normal exploration HUD.</summary>
        public static bool HidesExplorationHud { get; private set; }

        /// <summary>Gets the prompt for opening or closing the family portrait.</summary>
        public override string InteractionPrompt => isOpen
            ? HasAllFragments() ? CompletePrompt : ClosePrompt
            : OpenPrompt;

        private void Awake()
        {
            interactionCollider = GetComponent<BoxCollider2D>();
            frameRenderer = GetComponent<SpriteRenderer>();
            timelineUnlockSequence = FindAnyObjectByType<TimelineUnlockSequence>();
            interactionCollider.isTrigger = true;
            if (frameRenderer.sprite != null)
            {
                Vector2 spriteSize = frameRenderer.sprite.bounds.size;
                interactionCollider.size = spriteSize + Vector2.one * ColliderPadding;
                interactionCollider.offset = frameRenderer.sprite.bounds.center;
            }
        }

        /// <summary>Opens the portrait, closes it, or completes the game when all four fragments are present.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null || isEnding) return;

            if (!isOpen)
            {
                playerController = interactor.GetComponent<TopDownCharacterController>();
                playerController?.SetMovementEnabled(false);
                isOpen = true;
                HidesExplorationHud = true;
                return;
            }

            isOpen = false;
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && !gameManager.IsTimelineTravelUnlocked && timelineUnlockSequence != null)
            {
                isTimelineIntroPlaying = true;
                HidesExplorationHud = true;
                timelineUnlockSequence.Begin(playerController, this);
                return;
            }

            if (HasAllFragments())
            {
                isEnding = true;
                HidesExplorationHud = true;
                GameManager.Instance?.SetGameState(GameState.GameOver);
                return;
            }

            HidesExplorationHud = false;
            if (playerController != null) playerController.SetMovementEnabled(true);
        }

        /// <summary>Restores the exploration HUD after the first-watch discovery sequence ends.</summary>
        public void FinishTimelineIntro()
        {
            isTimelineIntroPlaying = false;
            HidesExplorationHud = false;
        }

        private void OnGUI()
        {
            if (!isOpen && !isEnding) return;
            EnsureGuiResources();
            DrawOverlay();
            if (isEnding)
            {
                DrawEndingScreen();
                return;
            }

            DrawFrameScreen();
        }

        private void DrawOverlay()
        {
            GUI.color = OverlayColor;
            GUI.DrawTexture(new Rect(Zero, Zero, Screen.width, Screen.height), solidTexture);
            GUI.color = Color.white;
        }

        private void DrawFrameScreen()
        {
            if (frameRenderer.sprite != null)
            {
                Rect spriteRect = frameRenderer.sprite.rect;
                float aspect = spriteRect.width / spriteRect.height;
                float imageHeight = Mathf.Min(Screen.height * ImageHeightRatio, Screen.height - PanelMargin * 2f - TextHeight - ImageTextGap);
                float imageWidth = Mathf.Min(Screen.width * ImageWidthRatio, imageHeight * aspect);
                imageHeight = imageWidth / aspect;
                float groupHeight = imageHeight + ImageTextGap + TextHeight;
                float imageY = (Screen.height - groupHeight) * 0.5f;
                Rect imageRect = new Rect((Screen.width - imageWidth) * 0.5f, imageY, imageWidth, imageHeight);
                DrawFrameSprite(imageRect);

                Rect textRect = new Rect(Screen.width * 0.08f, imageRect.yMax + ImageTextGap,
                    Screen.width * 0.84f, TextHeight);
                GameTextGUI.DrawLabel(textRect, NarrativeText, TextAnchor.MiddleCenter);
            }
        }

        private void DrawFrameSprite(Rect imageRect)
        {
            Texture2D spriteTexture = frameRenderer.sprite.texture;
            Rect textureRect = frameRenderer.sprite.textureRect;
            Rect uv = new Rect(
                textureRect.x / spriteTexture.width,
                textureRect.y / spriteTexture.height,
                textureRect.width / spriteTexture.width,
                textureRect.height / spriteTexture.height);
            GUI.DrawTextureWithTexCoords(imageRect, spriteTexture, uv, true);
        }

        private void DrawEndingScreen()
        {
            Rect rect = new Rect(Screen.width * 0.08f, Screen.height * 0.36f, Screen.width * 0.84f, Screen.height * 0.28f);
            GameTextGUI.DrawLabel(rect, "El retrato vuelve a estar completo.\nGracias por jugar.", TextAnchor.MiddleCenter);
        }

        private bool HasAllFragments()
        {
            for (int index = 0; index < FragmentIds.Length; index++)
            {
                if (GameManager.Instance == null || !GameManager.Instance.HasItem(FragmentIds[index])) return false;
            }
            return true;
        }

        private void EnsureGuiResources()
        {
            if (solidTexture != null) return;
            solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            solidTexture.SetPixel(0, 0, Color.white);
            solidTexture.Apply();
        }

        private void OnDisable()
        {
            if (!isTimelineIntroPlaying) HidesExplorationHud = false;
            if (isOpen && !isEnding && playerController != null)
            {
                playerController.SetMovementEnabled(true);
            }
        }

        private void OnDestroy()
        {
            if (solidTexture != null) Destroy(solidTexture);
        }
    }
}
