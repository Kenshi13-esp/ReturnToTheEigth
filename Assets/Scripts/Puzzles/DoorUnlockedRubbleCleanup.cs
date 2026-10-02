using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shakes the camera, clears Hall debris and shows story feedback when DoorH is permanently unlocked.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorUnlockedRubbleCleanup : MonoBehaviour
    {
        private const string DoorIdentifier = "DoorH";
        private const int DoorShakeCount = 3;
        private const float DoorShakeDuration = 0.24f;
        private const float DoorShakeMagnitude = 0.075f;
        private const float DoorShakeInterval = 0.12f;
        private const float StoryNoticeDuration = 7f;
        private const string EarthquakeStoryMessage = "It seems that, thanks to that earthquake, the stairs have cleared. Now I can go upstairs and use the sheet music with my family's old piano.";

        [SerializeField] private GameObject rubbleToDestroy;

        private DoorStateEventChannelSO doorStateChannel;
        private CameraShake cameraShake;
        private TimeTravelFeedback timeTravelFeedback;
        private bool hasHandledDoorOpening;

        private void Awake()
        {
            cameraShake = FindAnyObjectByType<CameraShake>();
            timeTravelFeedback = FindAnyObjectByType<TimeTravelFeedback>();
        }

        private void OnEnable()
        {
            GameManager gameManager = GameManager.Instance;
            doorStateChannel = gameManager != null ? gameManager.DoorStateChannel : null;
            if (doorStateChannel == null)
                return;

            doorStateChannel.OnDoorStateRequested += HandleDoorStateRequested;
            if (doorStateChannel.IsDoorPermanentlyOpen(DoorIdentifier))
                ClearRubble();
        }

        private void OnDisable()
        {
            if (doorStateChannel != null)
                doorStateChannel.OnDoorStateRequested -= HandleDoorStateRequested;
        }

        private void HandleDoorStateRequested(string requestedDoorIdentifier, bool shouldOpen)
        {
            if (hasHandledDoorOpening
                || !shouldOpen
                || requestedDoorIdentifier != DoorIdentifier
                || !doorStateChannel.IsDoorPermanentlyOpen(DoorIdentifier))
            {
                return;
            }

            hasHandledDoorOpening = true;
            if (cameraShake == null)
                cameraShake = FindAnyObjectByType<CameraShake>();

            if (cameraShake != null)
            {
                cameraShake.ShakeSequence(DoorShakeCount, DoorShakeDuration,
                    DoorShakeMagnitude, DoorShakeInterval);
            }

            ClearRubble();
            if (timeTravelFeedback == null)
                timeTravelFeedback = FindAnyObjectByType<TimeTravelFeedback>();

            timeTravelFeedback?.ShowStoryNotice(EarthquakeStoryMessage, StoryNoticeDuration);
        }

        private void ClearRubble()
        {
            if (rubbleToDestroy != null)
                Destroy(rubbleToDestroy);
        }
    }
}
