using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Clears Hall debris when DoorH is permanently unlocked so the staircase remains accessible.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorUnlockedRubbleCleanup : MonoBehaviour
    {
        private const string DoorIdentifier = "DoorH";

        [SerializeField] private GameObject rubbleToDestroy;

        private DoorStateEventChannelSO doorStateChannel;
        private bool hasHandledDoorOpening;

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
            ClearRubble();
        }

        private void ClearRubble()
        {
            if (rubbleToDestroy != null)
                Destroy(rubbleToDestroy);
        }
    }
}
