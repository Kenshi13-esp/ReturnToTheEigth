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
        private const float AnimationDurationSeconds = 0.3f;
        private const float AnimationOverlayOpacity = 0.75f;
        private const float HoverScaleMultiplier = 1.3f;

        [SerializeField] private Button button;
        [SerializeField] private Image animationOverlay;
        [SerializeField] private Sprite[] glassBreakageButton01Frames;
        [SerializeField] private Sprite[] glassBreakageButton02Frames;
        [SerializeField] private Sprite[] glassBreakageButton03Frames;
        [SerializeField] private UnityEvent onAnimationFinished = new UnityEvent();
        [SerializeField] private string sceneToLoadAfterAnimation;

        private Coroutine playbackCoroutine;
        private int originalSiblingIndex;
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

            animationOverlay.raycastTarget = false;
            animationOverlay.preserveAspect = false;
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
            transform.localScale = originalLocalScale;

            if (animationOverlay != null)
                animationOverlay.enabled = false;
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = originalLocalScale * HoverScaleMultiplier;
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
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

            transform.SetAsLastSibling();
            playbackCoroutine = StartCoroutine(PlayFrameSequence(frames));
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

            for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                animationOverlay.sprite = frames[frameIndex];
                yield return new WaitForSecondsRealtime(frameDurationSeconds);
            }

            onAnimationFinished?.Invoke();

            if (!string.IsNullOrWhiteSpace(sceneToLoadAfterAnimation))
            {
                AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneToLoadAfterAnimation, LoadSceneMode.Single);
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
            }

            animationOverlay.enabled = false;
            animationOverlay.sprite = null;
            RestoreButtonSiblingIndex();
            playbackCoroutine = null;
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
