using System.Collections;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Opens the family photo frame and completes the game once all four hidden fragments have been collected.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class FamilyPhotoFrameInteractable : InteractableBase
    {
        private const string OpenPrompt = "View the family portrait";
        private const string FrameExitActionPath = "Player/PuzzleExit";
        private const string MainMenuSceneName = "MainMenu";
        private const string MissingFrameExitActionWarning = "FamilyPhotoFrameInteractable could not find Player/PuzzleExit.";
        private const string MissingMainMenuWarning = "FamilyPhotoFrameInteractable could not load the MainMenu scene after the final cinematic.";
        private const string CompletePortraitMemory = "Now I remember everything. I was the one who started the fire when I was eight years old.";
        private const string InitialPortraitMessage = "This is the old family portrait. It seems intact, but our family photo is missing. This is very strange...";
        private const string ExitPortraitPromptFormat = "Press {0} to leave the portrait.";
        private const float InitialPortraitMessageDuration = 5f;
        private const float FinalPortraitMessageDuration = 2f;
        private const int PanelMargin = 24;
        private const float HighlightRadius = 0.9f;
        private const float ColliderPadding = 0.4f;
        private const float ImageWidthRatio = 0.54f;
        private const float ImageHeightRatio = 0.66f;
        private const float FrameVerticalOffset = 72f;
        private const float FramePanelGap = 16f;
        private const float Zero = 0f;
        private const float FullAudioVolume = 1f;
        private const float TwoDimensionalAudio = 0f;
        private const ushort CinematicAudioTrackIndex = 0;
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
        [SerializeField] private Sprite characterPicture;
        [SerializeField] private Sprite missingPortraitSprite;
        [SerializeField] private Sprite completedPortraitSprite;
        [SerializeField] private VideoClip finalCinematicClip;
        private bool isOpen;
        private bool isEnding;
        private bool isTimelineIntroPlaying;
        private bool hasOpenedFrame;
        private bool hasUsedFrameExit;
        private bool isShowingInitialPortraitMessage;
        private float initialPortraitMessageUntil;
        private VideoPlayer finalCinematicPlayer;
        private AudioSource finalCinematicAudioSource;
        private Coroutine finalEndingCoroutine;
        private bool isFinalCinematicPlaying;

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

            if (finalCinematicClip != null)
            {
                finalCinematicPlayer = gameObject.AddComponent<VideoPlayer>();
                finalCinematicPlayer.playOnAwake = false;
                finalCinematicPlayer.source = VideoSource.VideoClip;
                finalCinematicPlayer.clip = finalCinematicClip;
                finalCinematicPlayer.renderMode = VideoRenderMode.CameraNearPlane;
                finalCinematicPlayer.targetCamera = Camera.main;
                finalCinematicPlayer.aspectRatio = VideoAspectRatio.FitInside;
                finalCinematicAudioSource = gameObject.AddComponent<AudioSource>();
                finalCinematicAudioSource.playOnAwake = false;
                finalCinematicAudioSource.volume = FullAudioVolume;
                finalCinematicAudioSource.spatialBlend = TwoDimensionalAudio;
                AudioSettingsController.RegisterMusicSource(finalCinematicAudioSource);
                finalCinematicPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                finalCinematicPlayer.EnableAudioTrack(CinematicAudioTrackIndex, true);
                finalCinematicPlayer.SetTargetAudioSource(CinematicAudioTrackIndex, finalCinematicAudioSource);
                finalCinematicPlayer.isLooping = false;
                finalCinematicPlayer.loopPointReached += HandleFinalCinematicFinished;
            }
        }

        private void OnEnable()
        {
            if (isOpen) frameExitAction?.Enable();
        }

        private void Update()
        {
            if (isOpen && !HasAllFragments() && frameExitAction != null && frameExitAction.WasPerformedThisFrame())
            {
                hasUsedFrameExit = true;
                GameManager.Instance?.MarkFamilyFrameExitUsed();
                CloseFrame();
            }
        }

        /// <summary>Opens the family portrait; after completion it locks control and starts the timed ending sequence.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null || isEnding || isOpen) return;
            playerController = interactor.GetComponent<TopDownCharacterController>();


            bool hasAllFragments = HasAllFragments();
            if (!hasAllFragments)
            {
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
            }

            playerController?.SetMovementEnabled(false);
            isShowingInitialPortraitMessage = GameManager.Instance != null
                ? !GameManager.Instance.HasUsedFamilyFrameInteraction
                : !hasOpenedFrame;
            initialPortraitMessageUntil = Time.unscaledTime + InitialPortraitMessageDuration;
            hasOpenedFrame = true;
            GameManager.Instance?.MarkFamilyFrameInteractionUsed();
            isOpen = true;
            HidesExplorationHud = true;

            if (hasAllFragments)
            {
                GameManager.Instance?.SetGameState(GameState.Puzzle);
                finalEndingCoroutine = StartCoroutine(PlayFinalEndingSequence());
            }
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

            HidesExplorationHud = false;
            if (playerController != null) playerController.SetMovementEnabled(true);
        }

        private IEnumerator PlayFinalEndingSequence()
        {
            yield return new WaitForSecondsRealtime(FinalPortraitMessageDuration);
            finalEndingCoroutine = null;
            if (!isOpen)
            {
                yield break;
            }

            isOpen = false;
            isEnding = true;
            HidesExplorationHud = true;
            frameExitAction?.Disable();
            frameExitAction?.Dispose();
            frameExitAction = null;
            GameManager.Instance?.SetGameState(GameState.Puzzle);

            if (finalCinematicPlayer == null)
            {
                HandleFinalCinematicFinished(null);
                yield break;
            }

            isFinalCinematicPlaying = true;
            finalCinematicPlayer.targetCamera = Camera.main;
            AudioSettingsController.SuspendBackgroundMusicForCinematic();
            finalCinematicPlayer.Play();
        }

        /// <summary>Restores the exploration HUD after the first-watch discovery sequence ends.</summary>
        public void FinishTimelineIntro()
        {
            isTimelineIntroPlaying = false;
            HidesExplorationHud = false;
        }

        private void OnGUI()
        {
            if (isFinalCinematicPlaying || (!isOpen && !isEnding)) return;
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
            bool showingInitialMessage = isShowingInitialPortraitMessage
                && Time.unscaledTime < initialPortraitMessageUntil;
            string message = showingInitialMessage
                ? InitialPortraitMessage
                : HasAllFragments() ? CompletePortraitMemory : string.Empty;
            if (!HasAllFragments() && !showingInitialMessage && ShouldShowFrameExitHint)
            {
                if (!string.IsNullOrEmpty(message)) message += "\n";
                message += string.Format(ExitPortraitPromptFormat, InputPromptUtility.PuzzleExitControlLabel);
            }

            if (!string.IsNullOrEmpty(message))
            {
                GameTextGUI.DrawLabel(dialogueRect, message, dialogueBoxBackground, TextAnchor.MiddleCenter, characterPicture);
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

        private void HandleFinalCinematicFinished(VideoPlayer source)
        {
            isFinalCinematicPlaying = false;
            GameManager.Instance?.SetGameState(GameState.GameOver);
            MenuOptionsNavigation.ShowCreditsOnNextMainMenuLoad();
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            if (sceneLoad == null)
            {
                Debug.LogWarning(MissingMainMenuWarning, this);
            }
        }

        private void DrawEndingScreen()
        {
            Rect rect = GameTextGUI.GetStandardDialogueRect();
            GameTextGUI.DrawLabel(rect, "The family portrait is complete again.\nThank you for playing.", dialogueBoxBackground, TextAnchor.MiddleCenter, characterPicture);
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
            if (isOpen && !isEnding && !HasAllFragments() && playerController != null)
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
