using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Opens the family photo frame and completes the game once all four hidden fragments have been collected.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class FamilyPhotoFrameInteractable : InteractableBase
    {
        private const string OpenPrompt = "View the family portrait";
        private const string FrameExitActionPath = "Player/PuzzleExit";
        private const string MissingFrameExitActionWarning = "FamilyPhotoFrameInteractable could not find Player/PuzzleExit.";
        private const string CompletePortraitMemory = "Now I remember everything. I was the one who started the fire when I was eight years old.";
        private const string ExitPortraitPromptFormat = "Press {0} to leave the portrait.";
        private const int PanelMargin = 24;
        private const float HighlightRadius = 0.9f;
        private const float ColliderPadding = 0.4f;
        private const float ImageWidthRatio = 0.54f;
        private const float ImageHeightRatio = 0.66f;
        private const float FrameVerticalOffset = 72f;
        private const float FramePanelGap = 16f;
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
        private InputAction frameExitAction;
        private Texture2D solidTexture;
        [SerializeField] private Sprite dialogueBoxBackground;
        [SerializeField] private Sprite missingPortraitSprite;
        [SerializeField] private Sprite completedPortraitSprite;
        private bool isOpen;
        private bool isEnding;
        private bool isTimelineIntroPlaying;
        private bool hasOpenedFrame;
        private bool hasUsedFrameExit;

        /// <summary>Gets whether this view should hide the normal exploration HUD.</summary>
        public static bool HidesExplorationHud { get; private set; }

        /// <summary>Gets whether the first-time interaction instruction should be shown for this frame.</summary>
        public bool ShouldShowInitialInteractionHint => GameManager.Instance != null
            ? !GameManager.Instance.HasUsedFamilyFrameInteraction : !hasOpenedFrame;

        /// <summary>Gets whether the first-time exit instruction should be shown inside the frame.</summary>
        private bool ShouldShowFrameExitHint => GameManager.Instance != null
            ? !GameManager.Instance.HasUsedFamilyFrameExit : !hasUsedFrameExit;

        /// <summary>Gets the prompt used before the family portrait has been opened for the first time.</summary>
        public override string InteractionPrompt => OpenPrompt;

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

        private void OnEnable()
        {
            if (isOpen) frameExitAction?.Enable();
        }

        private void Update()
        {
            if (isOpen && frameExitAction != null && frameExitAction.WasPerformedThisFrame())
            {
                hasUsedFrameExit = true;
                GameManager.Instance?.MarkFamilyFrameExitUsed();
                CloseFrame();
            }
        }

        /// <summary>Opens the portrait on its first interaction; closing it is reserved for the Q/PuzzleExit action.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null || isEnding || isOpen) return;

            playerController = interactor.GetComponent<TopDownCharacterController>();
            PlayerInteraction playerInteraction = interactor.GetComponent<PlayerInteraction>();
            InputActionAsset inputActions = playerInteraction != null ? playerInteraction.InputActions : null;
            InputAction sourceExitAction = inputActions != null
                ? inputActions.FindAction(FrameExitActionPath, false) : null;
            frameExitAction = sourceExitAction != null ? sourceExitAction.Clone() : null;
            if (frameExitAction == null)
            {
                Debug.LogWarning(MissingFrameExitActionWarning, this);
                return;
            }

            frameExitAction.Enable();
            playerController?.SetMovementEnabled(false);
            hasOpenedFrame = true;
            GameManager.Instance?.MarkFamilyFrameInteractionUsed();
            isOpen = true;
            HidesExplorationHud = true;
        }

        private void CloseFrame()
        {
            isOpen = false;
            frameExitAction?.Disable();
            frameExitAction?.Dispose();
            frameExitAction = null;
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
            Sprite portraitSprite = GetPortraitSprite();
            if (portraitSprite == null) return;

            Rect spriteRect = portraitSprite.rect;
            float aspect = spriteRect.width / spriteRect.height;
            Rect dialogueRect = GameTextGUI.GetStandardDialogueRect();
            float maximumImageHeight = 2f * (dialogueRect.yMin - FramePanelGap + FrameVerticalOffset) - Screen.height;
            float imageHeight = Mathf.Min(Screen.height * ImageHeightRatio,
                Mathf.Min(Screen.height - PanelMargin * 2f, maximumImageHeight));
            float imageWidth = Mathf.Min(Screen.width * ImageWidthRatio, imageHeight * aspect);
            imageHeight = imageWidth / aspect;
            Rect imageRect = new Rect((Screen.width - imageWidth) * 0.5f,
                (Screen.height - imageHeight) * 0.5f - FrameVerticalOffset, imageWidth, imageHeight);
            DrawFrameSprite(imageRect, portraitSprite);
            DrawFrameDialogue(dialogueRect);
        }

        private void DrawFrameDialogue(Rect dialogueRect)
        {
            string message = HasAllFragments() ? CompletePortraitMemory : string.Empty;
            if (ShouldShowFrameExitHint)
            {
                if (!string.IsNullOrEmpty(message)) message += "\n";
                message += string.Format(ExitPortraitPromptFormat, InputPromptUtility.PuzzleExitControlLabel);
            }

            if (!string.IsNullOrEmpty(message))
            {
                GameTextGUI.DrawLabel(dialogueRect, message, dialogueBoxBackground, TextAnchor.MiddleCenter);
            }
        }

        private Sprite GetPortraitSprite()
        {
            bool hasAllFragments = HasAllFragments();
            Sprite selectedSprite = hasAllFragments ? completedPortraitSprite : missingPortraitSprite;
            return selectedSprite != null ? selectedSprite : frameRenderer.sprite;
        }

        private void DrawFrameSprite(Rect imageRect, Sprite sprite)
        {
            Texture2D spriteTexture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(
                textureRect.x / spriteTexture.width,
                textureRect.y / spriteTexture.height,
                textureRect.width / spriteTexture.width,
                textureRect.height / spriteTexture.height);
            GUI.DrawTextureWithTexCoords(imageRect, spriteTexture, uv, true);
        }

        private void DrawEndingScreen()
        {
            Rect rect = GameTextGUI.GetStandardDialogueRect();
            GameTextGUI.DrawLabel(rect, "The family portrait is complete again.\nThank you for playing.", dialogueBoxBackground, TextAnchor.MiddleCenter);
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
            frameExitAction?.Disable();
            if (!isTimelineIntroPlaying) HidesExplorationHud = false;
            if (isOpen && !isEnding && playerController != null)
            {
                playerController.SetMovementEnabled(true);
            }
        }

        private void OnDestroy()
        {
            frameExitAction?.Dispose();
            if (solidTexture != null) Destroy(solidTexture);
        }
    }
}
