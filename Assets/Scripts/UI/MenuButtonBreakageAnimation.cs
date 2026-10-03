using System.Collections;
using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ReturnToTheEigth.UI
{
    /// <summary>Plays one random glass-break animation over a menu button after each click.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class MenuButtonBreakageAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const string HallSceneName = "Hall";
        private const string GlassBreakSoundEffectId = "break-cristal";
        private const int AnimationVariantCount = 3;
        private const int FirstVariantIndex = 0;
        private const int SecondVariantIndex = 1;
        private const int ThirdVariantIndex = 2;
        private const float AnimationDurationSeconds = 0.4f;
        private const float CinematicPrepareTimeoutSeconds = 15f;
        private const float CinematicPlaybackTimeoutGraceSeconds = 30f;
        private const float AnimationOverlayOpacity = 0.75f;
        private const float HoverScaleMultiplier = 1.12f;
        private const float FullAudioVolume = 1f;
        private const float TwoDimensionalAudio = 0f;
        private const ushort CinematicAudioTrackIndex = 0;
       

        [SerializeField] private Button button;
        [SerializeField] private Image animationOverlay;
        [SerializeField] private Sprite[] glassBreakageButton01Frames;
        [SerializeField] private Sprite[] glassBreakageButton02Frames;
        [SerializeField] private Sprite[] glassBreakageButton03Frames;
        [SerializeField] private UnityEvent onAnimationFinished = new UnityEvent();
        [SerializeField] private string sceneToLoadAfterAnimation;
        [SerializeField] private bool enablePointerHoverFeedback;
        [SerializeField] private VideoClip introCinematicClip;

        private Coroutine playbackCoroutine;
        private RectTransform animationOverlayRectTransform;
        private Image buttonImage;
        private AudioSource introCinematicAudioSource;
        private Canvas menuCanvas;
        private Canvas[] menuCanvases;
        private bool[] menuCanvasEnabledStates;
        private Vector2 originalOverlaySizeDelta;
        private bool isMenuCanvasHiddenForCinematic;
        private bool isLoadingDestinationScene;
        private VideoPlayer introVideoPlayer;

        private int originalSiblingIndex;
        private bool isPointerOverButton;
        private bool isSelected;
        private bool hasOriginalSiblingIndex;
        private bool isSiblingIndexRestorePending;
        private Vector3 originalLocalScale;

        private void Awake()
        {
            originalLocalScale = transform.localScale;
            menuCanvas = GetComponentInParent<Canvas>();

            if (button == null)
                button = GetComponent<Button>();

            if (animationOverlay == null)
            {
                Debug.LogWarning("MenuButtonBreakageAnimation requires an overlay Image.", this);
                enabled = false;
                return;
            }

            animationOverlayRectTransform = animationOverlay.rectTransform;
            originalOverlaySizeDelta = animationOverlayRectTransform.sizeDelta;
            buttonImage = button.GetComponent<Image>();
            animationOverlay.raycastTarget = false;
            // Previous fixed button-rectangle behavior retained for rollback:
            // animationOverlayRectTransform.sizeDelta = originalOverlaySizeDelta;
            animationOverlay.preserveAspect = true;
            Color overlayColor = animationOverlay.color;
            overlayColor.a = AnimationOverlayOpacity;
            animationOverlay.color = overlayColor;
            animationOverlay.enabled = false;
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(PlayRandomBreakageAnimation);

            RestorePendingSiblingIndex();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(PlayRandomBreakageAnimation);

            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            if (!isLoadingDestinationScene)
            {
                StopAndDestroyIntroVideoPlayer();
                AudioSettingsController.ResumeAudioAfterCinematic();
                RestoreMenuCanvasAfterCinematic();
            }

            RestoreButtonSiblingIndex();
            RestoreAnimationOverlaySize();
            transform.localScale = originalLocalScale;
            isPointerOverButton = false;
            isSelected = false;

            if (animationOverlay != null)
                animationOverlay.enabled = false;
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            if (!enablePointerHoverFeedback)
                return;

            isPointerOverButton = true;
            ApplyFocusFeedback();
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            if (!enablePointerHoverFeedback)
                return;

            isPointerOverButton = false;
            ApplyFocusFeedback();
        }

        void ISelectHandler.OnSelect(BaseEventData eventData)
        {
            isSelected = true;
            ApplyFocusFeedback();
        }

        void IDeselectHandler.OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            ApplyFocusFeedback();
        }

        private void ApplyFocusFeedback()
        {
            if (playbackCoroutine != null)
                return;

            bool isFocused = isSelected || enablePointerHoverFeedback && isPointerOverButton;
            transform.localScale = isFocused
                ? originalLocalScale * HoverScaleMultiplier
                : originalLocalScale;
        }

        private void PlayRandomBreakageAnimation()
        {
            Sprite[] frames = SelectRandomFrameSequence();
            if (animationOverlay == null || frames == null || frames.Length == 0)
            {
                Debug.LogWarning("No glass-break animation frames are assigned to this menu button.", this);
                return;
            }

            SoundManager.Play(GlassBreakSoundEffectId);

            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
            }
            else
            {
                originalSiblingIndex = transform.GetSiblingIndex();
                hasOriginalSiblingIndex = true;
            }

            transform.localScale = originalLocalScale;
            transform.SetAsLastSibling();
            KeepGamepadCursorAboveButton();
            playbackCoroutine = StartCoroutine(PlayFrameSequence(frames));
        }

        private void KeepGamepadCursorAboveButton()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                return;

            Transform gamepadCursor = parentCanvas.transform.Find("GamepadCursor");
            if (gamepadCursor != null)
                gamepadCursor.SetAsLastSibling();
        }

        private Sprite[] SelectRandomFrameSequence()
        {
            int variantIndex = Random.Range(FirstVariantIndex, AnimationVariantCount);
            return variantIndex switch
            {
                FirstVariantIndex => glassBreakageButton01Frames,
                SecondVariantIndex => glassBreakageButton02Frames,
                ThirdVariantIndex => glassBreakageButton03Frames,
                _ => null
            };
        }

        private IEnumerator PlayFrameSequence(Sprite[] frames)
        {
            animationOverlay.enabled = true;
            animationOverlay.sprite = null;
            float frameDurationSeconds = AnimationDurationSeconds / frames.Length;
            Vector2 displayedButtonSize = CalculateDisplayedButtonSize();
            float referenceFrameHeight = frames[0].rect.height;
            float uiUnitsPerSourcePixel = referenceFrameHeight > 0
                ? displayedButtonSize.y / referenceFrameHeight
                : 0;

            // Previous fixed overlay sizing retained for rollback:
            // animationOverlayRectTransform.sizeDelta = originalOverlaySizeDelta;
            for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                animationOverlay.sprite = frames[frameIndex];
                SetOverlaySizeForFrame(frames[frameIndex], uiUnitsPerSourcePixel);
                yield return new WaitForSecondsRealtime(frameDurationSeconds);
            }

            onAnimationFinished?.Invoke();

            animationOverlay.enabled = false;
            animationOverlay.sprite = null;
            RestoreAnimationOverlaySize();
            RestoreButtonSiblingIndex();

            if (!string.IsNullOrWhiteSpace(sceneToLoadAfterAnimation))
            {
                if (introCinematicClip != null)
                    yield return PlayIntroCinematic();

                if (sceneToLoadAfterAnimation == HallSceneName)
                    GameManager.Instance?.ResetForNewGameSession();

                isLoadingDestinationScene = true;
                AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneToLoadAfterAnimation, LoadSceneMode.Single);
                if (sceneLoad == null)
                {
                    isLoadingDestinationScene = false;
                    RestoreMenuCanvasAfterCinematic();
                    AudioSettingsController.ResumeAudioAfterCinematic();
                    Debug.LogError($"Menu cinematic could not load scene '{sceneToLoadAfterAnimation}'.", this);
                }
                else
                {
                    while (!sceneLoad.isDone)
                        yield return null;

                    if (SceneManager.GetActiveScene().name != sceneToLoadAfterAnimation)
                    {
                        isLoadingDestinationScene = false;
                        RestoreMenuCanvasAfterCinematic();
                        AudioSettingsController.ResumeAudioAfterCinematic();
                        Debug.LogError($"Scene load completed without activating '{sceneToLoadAfterAnimation}'.", this);
                    }
                }
            }

            playbackCoroutine = null;
            ApplyFocusFeedback();
        }

        private IEnumerator PlayIntroCinematic()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("MenuButtonBreakageAnimation could not play the intro cinematic because there is no MainCamera.", this);
                yield break;
            }

            introVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            introVideoPlayer.playOnAwake = false;
            introVideoPlayer.source = VideoSource.VideoClip;
            introVideoPlayer.clip = introCinematicClip;
            introVideoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            introVideoPlayer.targetCamera = mainCamera;
            introVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            introVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            if (introCinematicAudioSource == null)
            {
                introCinematicAudioSource = gameObject.AddComponent<AudioSource>();
                introCinematicAudioSource.playOnAwake = false;
                introCinematicAudioSource.volume = FullAudioVolume;
                introCinematicAudioSource.spatialBlend = TwoDimensionalAudio;
                AudioSettingsController.RegisterMusicSource(introCinematicAudioSource);
            }
            introVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            introVideoPlayer.EnableAudioTrack(CinematicAudioTrackIndex, true);
            introVideoPlayer.SetTargetAudioSource(CinematicAudioTrackIndex, introCinematicAudioSource);
            introVideoPlayer.isLooping = false;

            bool playbackFinished = false;
            bool playbackFailed = false;
            introVideoPlayer.loopPointReached += _ => playbackFinished = true;
            introVideoPlayer.errorReceived += (_, errorMessage) =>
            {
                Debug.LogError($"Intro cinematic playback failed: {errorMessage}", this);
                playbackFailed = true;
            };

            introVideoPlayer.Prepare();
            float preparationDeadline = Time.realtimeSinceStartup + CinematicPrepareTimeoutSeconds;
            while (!introVideoPlayer.isPrepared && !playbackFailed
                   && Time.realtimeSinceStartup < preparationDeadline)
            {
                yield return null;
            }

            if (!playbackFailed && !introVideoPlayer.isPrepared)
            {
                Debug.LogError("Intro cinematic preparation timed out.", this);
                playbackFailed = true;
            }

            if (!playbackFailed)
            {
                HideMenuCanvasForCinematic();
                AudioSettingsController.SuspendAudioForCinematic(introCinematicAudioSource);
                introVideoPlayer.Play();
                double playbackDeadline = Time.realtimeSinceStartup
                    + introCinematicClip.length + CinematicPlaybackTimeoutGraceSeconds;
                while (!playbackFinished && !playbackFailed
                       && Time.realtimeSinceStartup < playbackDeadline)
                {
                    yield return null;
                }

                if (!playbackFinished && !playbackFailed)
                {
                    Debug.LogError("Intro cinematic playback timed out; continuing to the destination scene.", this);
                }
            }

            StopAndDestroyIntroVideoPlayer();
            yield return null;
        }

        private void StopAndDestroyIntroVideoPlayer()
        {
            if (introVideoPlayer != null)
            {
                introVideoPlayer.Stop();
                introVideoPlayer.clip = null;
                Destroy(introVideoPlayer);
                introVideoPlayer = null;
            }

            if (introCinematicAudioSource != null)
                introCinematicAudioSource.Stop();
        }


        private void HideMenuCanvasForCinematic()
        {
            if (menuCanvas == null)
                menuCanvas = GetComponentInParent<Canvas>();

            if (menuCanvas == null || isMenuCanvasHiddenForCinematic)
                return;

            menuCanvases = menuCanvas.GetComponentsInChildren<Canvas>(true);
            menuCanvasEnabledStates = new bool[menuCanvases.Length];
            for (int canvasIndex = 0; canvasIndex < menuCanvases.Length; canvasIndex++)
            {
                Canvas canvas = menuCanvases[canvasIndex];
                if (canvas == null)
                    continue;

                menuCanvasEnabledStates[canvasIndex] = canvas.enabled;
                canvas.enabled = false;
            }

            isMenuCanvasHiddenForCinematic = true;
        }

        private void RestoreMenuCanvasAfterCinematic()
        {
            if (!isMenuCanvasHiddenForCinematic || menuCanvases == null)
                return;

            for (int canvasIndex = 0; canvasIndex < menuCanvases.Length; canvasIndex++)
            {
                Canvas canvas = menuCanvases[canvasIndex];
                if (canvas != null)
                    canvas.enabled = menuCanvasEnabledStates[canvasIndex];
            }

            menuCanvases = null;
            menuCanvasEnabledStates = null;
            isMenuCanvasHiddenForCinematic = false;
        }

        private Vector2 CalculateDisplayedButtonSize()
        {
            RectTransform buttonRectTransform = button.transform as RectTransform;
            if (buttonRectTransform == null)
                return Vector2.zero;

            Vector2 displayedSize = buttonRectTransform.rect.size;
            if (buttonImage == null || buttonImage.sprite == null || !buttonImage.preserveAspect)
                return displayedSize;

            Rect spriteRect = buttonImage.sprite.rect;
            if (spriteRect.width <= 0 || spriteRect.height <= 0 || displayedSize.x <= 0 || displayedSize.y <= 0)
                return displayedSize;

            float spriteAspectRatio = spriteRect.width / spriteRect.height;
            float rectAspectRatio = displayedSize.x / displayedSize.y;

            if (rectAspectRatio > spriteAspectRatio)
                displayedSize.x = displayedSize.y * spriteAspectRatio;
            else
                displayedSize.y = displayedSize.x / spriteAspectRatio;

            return displayedSize;
        }

        private void SetOverlaySizeForFrame(Sprite frame, float uiUnitsPerSourcePixel)
        {
            if (animationOverlayRectTransform == null || frame == null || uiUnitsPerSourcePixel <= 0)
                return;

            Vector2 frameSize = frame.rect.size * uiUnitsPerSourcePixel;
            animationOverlayRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, frameSize.x);
            animationOverlayRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, frameSize.y);
        }

        private void RestoreAnimationOverlaySize()
        {
            if (animationOverlayRectTransform != null)
                animationOverlayRectTransform.sizeDelta = originalOverlaySizeDelta;
        }

        private void RestoreButtonSiblingIndex()
        {
            if (!hasOriginalSiblingIndex)
                return;

            // SetSiblingIndex is not allowed while Unity activates or deactivates a parent,
            // e.g. when this OnDisable runs because MenuBackground is being deactivated.
            if (!gameObject.activeInHierarchy)
            {
                isSiblingIndexRestorePending = true;
                return;
            }

            RestorePendingSiblingIndex();
        }

        private void RestorePendingSiblingIndex()
        {
            if (!isSiblingIndexRestorePending)
                return;

            isSiblingIndexRestorePending = false;

            Transform buttonParent = transform.parent;
            if (buttonParent != null)
                transform.SetSiblingIndex(Mathf.Clamp(originalSiblingIndex, 0, buttonParent.childCount - 1));

            hasOriginalSiblingIndex = false;
        }
    }
}
