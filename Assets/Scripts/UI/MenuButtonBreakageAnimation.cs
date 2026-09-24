using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Plays one random glass-break animation over a menu button after each click.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class MenuButtonBreakageAnimation : MonoBehaviour
    {
        private const int AnimationVariantCount = 3;
        private const int FirstVariantIndex = 0;
        private const int SecondVariantIndex = 1;
        private const int ThirdVariantIndex = 2;
        private const float AnimationDurationSeconds = 0.3f;
        private const float FinalFrameHoldSeconds = 0.2f;

        [SerializeField] private Button button;
        [SerializeField] private Image animationOverlay;
        [SerializeField] private Sprite[] glassBreakageButton01Frames;
        [SerializeField] private Sprite[] glassBreakageButton02Frames;
        [SerializeField] private Sprite[] glassBreakageButton03Frames;
        [SerializeField] private UnityEvent onAnimationFinished = new UnityEvent();

        private Coroutine playbackCoroutine;

        private void Awake()
        {
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
            animationOverlay.enabled = false;
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(PlayRandomBreakageAnimation);
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

            if (animationOverlay != null)
                animationOverlay.enabled = false;
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
                StopCoroutine(playbackCoroutine);

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

            animationOverlay.sprite = frames[frames.Length - 1];
            yield return new WaitForSecondsRealtime(FinalFrameHoldSeconds);
            onAnimationFinished?.Invoke();
            animationOverlay.enabled = false;
            animationOverlay.sprite = null;
            playbackCoroutine = null;
        }
    }
}
