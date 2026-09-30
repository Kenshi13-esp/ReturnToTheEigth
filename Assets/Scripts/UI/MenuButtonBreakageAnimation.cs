using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Plays one random glass-break animation over a menu button after each click.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class MenuButtonBreakageAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const int AnimationVariantCount = 3;
        private const int FirstVariantIndex = 0;
        private const int SecondVariantIndex = 1;
        private const int ThirdVariantIndex = 2;
        private const float AnimationDurationSeconds = 0.4f;
        private const float AnimationOverlayOpacity = 0.75f;
        private const float HoverScaleMultiplier = 1.12f;
       

        [SerializeField] private Button button;
        [SerializeField] private Image animationOverlay;
        [SerializeField] private Sprite[] glassBreakageButton01Frames;
        [SerializeField] private Sprite[] glassBreakageButton02Frames;
        [SerializeField] private Sprite[] glassBreakageButton03Frames;
        [SerializeField] private UnityEvent onAnimationFinished = new UnityEvent();
        [SerializeField] private string sceneToLoadAfterAnimation;
        [SerializeField] private bool enablePointerHoverFeedback;

        private Coroutine playbackCoroutine;
        private RectTransform animationOverlayRectTransform;
        private Image buttonImage;
        private Vector2 originalOverlaySizeDelta;

        private int originalSiblingIndex;
        private bool isPointerOverButton;
        private bool hasOriginalSiblingIndex;
        private bool isSiblingIndexRestorePending;
        private Vector3 originalLocalScale;

        private void Awake()
        {
            originalLocalScale = transform.localScale;

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

            RestoreButtonSiblingIndex();
            RestoreAnimationOverlaySize();
            transform.localScale = originalLocalScale;
            isPointerOverButton = false;

            if (animationOverlay != null)
                animationOverlay.enabled = false;
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            if (!enablePointerHoverFeedback)
                return;

            isPointerOverButton = true;
            if (playbackCoroutine == null)
                transform.localScale = originalLocalScale * HoverScaleMultiplier;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            if (!enablePointerHoverFeedback)
                return;

            isPointerOverButton = false;
            transform.localScale = originalLocalScale;
        }

        private void PlayRandomBreakageAnimation()
        {
            Sprite[] frames = SelectRandomFrameSequence();
            if (animationOverlay == null || frames == null || frames.Length == 0)
            {
                Debug.LogWarning("No glass-break animation frames are assigned to this menu button.", this);
                return;
            }

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

            if (!string.IsNullOrWhiteSpace(sceneToLoadAfterAnimation))
            {
                AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneToLoadAfterAnimation, LoadSceneMode.Single);
                if (sceneLoad != null)
                {
                    while (!sceneLoad.isDone)
                        yield return null;
                }

                /* Previous behavior retained for rollback: it replayed the same break frames
                   repeatedly while the destination scene was loading.
                if (sceneLoad != null)
                {
                    int transitionFrameIndex = 0;
                    while (!sceneLoad.isDone)
                    {
                        animationOverlay.sprite = frames[transitionFrameIndex];
                        yield return new WaitForSecondsRealtime(frameDurationSeconds);
                        transitionFrameIndex = (transitionFrameIndex + 1) % frames.Length;
                    }
                }
                */
            }

            animationOverlay.enabled = false;
            animationOverlay.sprite = null;
            RestoreAnimationOverlaySize();
            RestoreButtonSiblingIndex();
            transform.localScale = enablePointerHoverFeedback && isPointerOverButton
                ? originalLocalScale * HoverScaleMultiplier
                : originalLocalScale;
            playbackCoroutine = null;
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
