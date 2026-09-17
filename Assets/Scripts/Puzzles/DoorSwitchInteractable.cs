using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Example puzzle input with no direct reference to its target door.</summary>
    public sealed class DoorSwitchInteractable : InteractableBase
    {
        private const string DefaultDoorIdentifier = "HallDoor";
        private const string ActivatePrompt = "Pull lever - open passage";
        private const string DeactivatePrompt = "Reset lever - close passage";
        private const float ActivatedAngle = -40f;
        private const float Zero = 0f;

        [SerializeField] private DoorStateEventChannelSO doorStateChannel;
        [SerializeField] private string doorIdentifier = DefaultDoorIdentifier;
        [SerializeField] private Transform leverHandle;
        private Quaternion restingRotation;
        private bool IsActivated => doorStateChannel != null
            && doorStateChannel.TryGetDoorState(doorIdentifier, out bool shouldOpen) && shouldOpen;
        public override string InteractionPrompt => IsActivated ? DeactivatePrompt : ActivatePrompt;

        private void Awake()
        {
            restingRotation = leverHandle != null ? leverHandle.localRotation : Quaternion.identity;
        }

        private void OnEnable()
        {
            if (doorStateChannel != null)
            {
                doorStateChannel.OnDoorStateRequested += HandleDoorStateChanged;
            }
            RefreshVisual();
        }

        private void OnDisable()
        {
            if (doorStateChannel != null)
            {
                doorStateChannel.OnDoorStateRequested -= HandleDoorStateChanged;
            }
        }

        /// <summary>Sends the opposite of the latest door state through the shared event channel.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor != null)
            {
                doorStateChannel?.RequestDoorState(doorIdentifier, !IsActivated);
            }
        }

        private void HandleDoorStateChanged(string identifier, bool shouldOpen)
        {
            if (identifier == doorIdentifier)
            {
                RefreshVisual();
            }
        }

        private void RefreshVisual()
        {
            if (leverHandle != null)
            {
                leverHandle.localRotation = restingRotation
                    * Quaternion.Euler(Zero, Zero, IsActivated ? ActivatedAngle : Zero);
            }
        }
    }
}
