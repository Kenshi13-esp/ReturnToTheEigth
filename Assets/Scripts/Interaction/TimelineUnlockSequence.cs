using System.Collections;
using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.TimeTravel;
using ReturnToTheEigth.UI;
using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Plays the first-frame discovery sequence outside the era roots so its camera shake and visual flashes survive era swaps.</summary>
    [DisallowMultipleComponent]
    public sealed class TimelineUnlockSequence : MonoBehaviour
    {
        private const string FirstMessage = "Mi reloj está actuando raro.";
        private static string RevealMessage => string.Format("Pulsa {0} para usar el poder del reloj.",
            InputPromptUtility.TimeShiftControlLabel);
        private const float FirstMessageDuration = 2.2f;
        private const float InitialFlashInterval = 1.25f;
        private const float FinalFlashInterval = 0.24f;
        private const float RevealMessageDuration = 4.6f;
        private const float MaximumCameraShakeDuration = 0.58f;
        private const float MinimumCameraShakeIntervalRatio = 0.72f;
        private const float InitialCameraShakeMagnitude = 0.045f;
        private const float FinalCameraShakeMagnitude = 0.085f;
        private const float FlashAccelerationPower = 2.8f;
        private const float PanelWidthRatio = 0.82f;
        private const float PanelHeight = 116f;
        private const float PanelBottom = 24f;
        private const int FlashCount = 8;
        private const int FirstIndex = 0;

        [SerializeField] private CameraShake cameraShake;

        private TimeTravelManager timeTravelManager;
        private Coroutine sequenceRoutine;
        private string currentMessage;
        private TopDownCharacterController playerController;
        private FamilyPhotoFrameInteractable frameInteractable;
        private TimelineEra startingEra;

        private void Awake()
        {
            timeTravelManager = FindAnyObjectByType<TimeTravelManager>();
            if (cameraShake == null) cameraShake = FindAnyObjectByType<CameraShake>();
        }

        /// <summary>Locks exploration and starts the one-time watch malfunction/revelation sequence.</summary>
        public void Begin(TopDownCharacterController controller, FamilyPhotoFrameInteractable frame)
        {
            if (sequenceRoutine != null || controller == null || frame == null) return;

            playerController = controller;
            frameInteractable = frame;
            startingEra = timeTravelManager != null
                ? timeTravelManager.CurrentEra
                : TimelineEra.Present;
            GameManager.Instance?.SetGameState(GameState.Puzzle);
            playerController.SetMovementEnabled(false);
            sequenceRoutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            currentMessage = FirstMessage;
            yield return new WaitForSecondsRealtime(FirstMessageDuration);

            for (int flashIndex = FirstIndex; flashIndex < FlashCount; flashIndex++)
            {
                TimelineEra previewEra = flashIndex % 2 == 0
                    ? GetOppositeEra(startingEra)
                    : startingEra;
                SetPreviewEra(previewEra);
                float progress = flashIndex / (FlashCount - 1f);
                float acceleratingProgress = Mathf.Pow(progress, FlashAccelerationPower);
                float flashInterval = Mathf.Lerp(InitialFlashInterval, FinalFlashInterval, acceleratingProgress);
                float shakeDuration = Mathf.Min(MaximumCameraShakeDuration, flashInterval * MinimumCameraShakeIntervalRatio);
                float shakeMagnitude = Mathf.Lerp(InitialCameraShakeMagnitude, FinalCameraShakeMagnitude, progress);
                if (cameraShake != null) cameraShake.Shake(shakeDuration, shakeMagnitude);
                yield return new WaitForSecondsRealtime(flashInterval);
            }
            RestoreEraRoots();

            currentMessage = RevealMessage;
            yield return new WaitForSecondsRealtime(RevealMessageDuration);

            GameManager gameManager = GameManager.Instance;
            gameManager?.UnlockTimelineTravel();
            gameManager?.SetGameState(GameState.Exploration);
            if (playerController != null) playerController.SetMovementEnabled(true);
            if (frameInteractable != null) frameInteractable.FinishTimelineIntro();

            currentMessage = null;
            sequenceRoutine = null;
            playerController = null;
            frameInteractable = null;
        }

        private void SetPreviewEra(TimelineEra era)
        {
            if (timeTravelManager != null)
            {
                timeTravelManager.SetEraForCinematic(era);
            }
        }

        private void RestoreEraRoots()
        {
            if (timeTravelManager != null) timeTravelManager.SetEraForCinematic(startingEra);
        }

        private static TimelineEra GetOppositeEra(TimelineEra era)
        {
            return era == TimelineEra.Present ? TimelineEra.Past : TimelineEra.Present;
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(currentMessage)) return;
            float panelWidth = Screen.width * PanelWidthRatio;
            Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f,
                Screen.height - PanelHeight - PanelBottom, panelWidth, PanelHeight);
            GameTextGUI.DrawLabel(panelRect, currentMessage, TextAnchor.MiddleCenter);
        }

        private void OnDisable()
        {
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
                RestoreEraRoots();
                if (cameraShake != null) cameraShake.Stop();
                if (playerController != null) playerController.SetMovementEnabled(true);
                GameManager.Instance?.SetGameState(GameState.Exploration);
                if (frameInteractable != null) frameInteractable.FinishTimelineIntro();
            }
        }

    }
}
