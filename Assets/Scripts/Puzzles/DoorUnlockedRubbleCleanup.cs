using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shakes the camera and removes Hall debris when DoorH is permanently unlocked.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorUnlockedRubbleCleanup : MonoBehaviour
    {
        private const string DoorIdentifier = "DoorH";
        private const int DoorShakeCount = 3;
        private const float DoorShakeDuration = 0.24f;
        private const float DoorShakeMagnitude = 0.075f;
        private const float DoorShakeInterval = 0.12f;

        private DoorStateEventChannelSO doorStateChannel;
        private CameraShake cameraShake;

        private void Awake()
        {
            cameraShake = FindAnyObjectByType<CameraShake>();
        }

        private void OnEnable()
        {
            GameManager gameManager = GameManager.Instance;
            doorStateChannel = gameManager != null ? gameManager.DoorStateChannel : null;
            if (doorStateChannel == null) return;

            doorStateChannel.OnDoorStateRequested += HandleDoorStateRequested;
            if (doorStateChannel.IsDoorPermanentlyOpen(DoorIdentifier))
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            if (doorStateChannel != null)
            {
                doorStateChannel.OnDoorStateRequested -= HandleDoorStateRequested;
            }
        }

        private void HandleDoorStateRequested(string requestedDoorIdentifier, bool shouldOpen)
        {
            if (!shouldOpen
                || requestedDoorIdentifier != DoorIdentifier
                || !doorStateChannel.IsDoorPermanentlyOpen(DoorIdentifier))
            {
                return;
            }

            if (cameraShake == null)
            {
                cameraShake = FindAnyObjectByType<CameraShake>();
            }

            if (cameraShake != null)
            {
                cameraShake.ShakeSequence(DoorShakeCount, DoorShakeDuration,
                    DoorShakeMagnitude, DoorShakeInterval);
            }

            Destroy(gameObject);
        }
    }
}
