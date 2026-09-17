using System;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>A channel-driven door with a stationary blocker and independently animated visual hinge.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorController : InteractableBase, ITimelineObstacle
    {
        private const string DefaultDoorIdentifier = "HallDoor";
        private const string OpenPrompt = "Open door";
        private const string ClosePrompt = "Close door";
        private const string LockedPrompt = "Locked - use the lever";
        private const string InvalidSetupError = "DoorController requires an identifier, channel, and stationary blocking collider.";
        private const float DefaultOpenAngle = -100f;
        private const float DefaultAnimationSpeed = 180f;
        private const float Zero = 0f;

        [SerializeField] private string doorIdentifier = DefaultDoorIdentifier;
        [SerializeField] private DoorStateEventChannelSO doorStateChannel;
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private Transform visualHinge;
        [SerializeField] private bool initiallyOpen;
        [SerializeField] private bool allowDirectInteraction = true;
        [SerializeField] private float openAngleDegrees = DefaultOpenAngle;
        [SerializeField, Min(Zero)] private float animationSpeedDegrees = DefaultAnimationSpeed;

        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private bool hasInitialized;
        public bool IsOpen { get; private set; }
        public override string InteractionPrompt => !allowDirectInteraction ? LockedPrompt : IsOpen ? ClosePrompt : OpenPrompt;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(doorIdentifier) || doorStateChannel == null || blockingCollider == null)
            {
                Debug.LogError(InvalidSetupError, this);
                enabled = false;
                return;
            }
            closedRotation = visualHinge != null ? visualHinge.localRotation : Quaternion.identity;
            IsOpen = initiallyOpen;
            hasInitialized = true;
        }

        private void OnEnable()
        {
            if (!hasInitialized)
            {
                return;
            }
            doorStateChannel.OnDoorStateRequested += HandleDoorStateRequested;
            bool shouldOpen = doorStateChannel.TryGetDoorState(doorIdentifier, out bool requestedOpen)
                ? requestedOpen : IsOpen;
            ApplyDoorState(shouldOpen, true);
        }

        private void OnDisable()
        {
            if (doorStateChannel != null)
            {
                doorStateChannel.OnDoorStateRequested -= HandleDoorStateRequested;
            }
        }

        private void Update()
        {
            if (visualHinge != null)
            {
                visualHinge.localRotation = Quaternion.RotateTowards(visualHinge.localRotation,
                    targetRotation, animationSpeedDegrees * Time.deltaTime);
            }
        }

        /// <summary>Requests opening by identifier, updating matching doors in either era.</summary>
        public void OpenDoor()
        {
            doorStateChannel?.RequestDoorState(doorIdentifier, true);
        }

        /// <summary>Requests closing by identifier and restores the stationary blocking volume.</summary>
        public void CloseDoor()
        {
            doorStateChannel?.RequestDoorState(doorIdentifier, false);
        }

        /// <summary>Toggles an unlocked door when the player interacts with its separate trigger volume.</summary>
        public override void Interact(GameObject interactor)
        {
            if (!allowDirectInteraction || interactor == null)
            {
                return;
            }
            if (IsOpen)
            {
                CloseDoor();
            }
            else
            {
                OpenDoor();
            }
        }

        /// <summary>Includes pending channel commands in destination clearance, even before this door's first Awake.</summary>
        public bool WillBlockTimeline(Collider2D candidate)
        {
            if (candidate != blockingCollider)
            {
                return candidate.enabled;
            }
            bool shouldOpen = doorStateChannel != null
                && doorStateChannel.TryGetDoorState(doorIdentifier, out bool requestedOpen)
                    ? requestedOpen : hasInitialized ? IsOpen : initiallyOpen;
            return !shouldOpen;
        }

        private void HandleDoorStateRequested(string requestedIdentifier, bool shouldOpen)
        {
            if (string.Equals(requestedIdentifier, doorIdentifier, StringComparison.Ordinal))
            {
                ApplyDoorState(shouldOpen, false);
            }
        }

        private void ApplyDoorState(bool shouldOpen, bool snapVisual)
        {
            IsOpen = shouldOpen;
            blockingCollider.enabled = !shouldOpen;
            targetRotation = closedRotation * Quaternion.Euler(Zero, Zero, shouldOpen ? openAngleDegrees : Zero);
            if (snapVisual && visualHinge != null)
            {
                visualHinge.localRotation = targetRotation;
            }
        }
    }
}
