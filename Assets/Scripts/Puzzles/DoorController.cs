using System;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>A permanently opening, channel-driven door with an independently animated visual hinge.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorController : InteractableBase, ITimelineObstacle
    {
        private const string DefaultDoorIdentifier = "HallDoor";
        private const string OpenPrompt = "Puerta abierta";
        private const string LockedPrompt = "Cerrada - usa la palanca";
        private const string OneSidedPromptFormat = "Puerta unidireccional: solo se abre desde {0}";
        private const string RequiredItemPromptFormat = "Usa {0} para abrir la puerta";
        private const string MissingItemPromptFormat = "Cerrada: requiere {0}";
        private const string WrongSidePrompt = "Does not open from this side";
        private const string DefaultSideLabel = "el lado autorizado";
        private const string InvalidSetupErrorFormat = "DoorController '{0}' requires an identifier, channel, blocking collider, valid access configuration, and dedicated door asset root.";
        private const string InvalidAssetRootError = "DoorController asset root must be a dedicated non-scene-root object containing this door and its blocking collider, and must not contain another DoorController.";
        private const float DefaultOpenAngle = -100f;
        private const float DefaultAnimationSpeed = 180f;
        private const float Zero = 0f;
        private const float MinimumSideDot = 0.0001f;
        private const string OutlineShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        public const float DoorInteractionRadius = 0.25f;
        private const float InteractionHighlightRadius = DoorInteractionRadius;
        private const float InteractionOutlineWidth = 0.04f;
        private const int DefaultOutlineSortingOrder = 21;
        private const int OutlineSortingOrderOffset = 1;
        private const int OutlinePointCount = 4;
        private const float OutlineHorizontalInset = 0.035f;
        private const float OutlineBottomInset = 0.055f;
        private const float OutlineTopInset = 0.225f;
        private static readonly Color InteractionOutlineColor = Color.white;

        [SerializeField] private string doorIdentifier = DefaultDoorIdentifier;
        [SerializeField] private DoorStateEventChannelSO doorStateChannel;
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private Transform visualHinge;
        [SerializeField] private bool initiallyOpen;
        [SerializeField] private bool allowDirectInteraction = true;
        [SerializeField] private DoorAccessMode accessMode = DoorAccessMode.OneSided;
        [SerializeField] private Transform accessPlane;
        [SerializeField] private Vector2 localAccessAxis = Vector2.up;
        [SerializeField] private bool openFromPositiveSide = true;
        [SerializeField] private string authorizedSideLabel = DefaultSideLabel;
        [SerializeField] private string requiredItemId;
        [SerializeField] private string requiredItemDisplayName;
        [SerializeField] private GameObject doorAssetRoot;
        [SerializeField] private float openAngleDegrees = DefaultOpenAngle;
        [SerializeField, Min(Zero)] private float animationSpeedDegrees = DefaultAnimationSpeed;
        [SerializeField] private PlayerInteraction playerInteraction;

        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private LineRenderer interactionOutline;
        private SpriteRenderer outlineBoundsRenderer;
        private Material runtimeOutlineMaterial;
        private bool isInteractionOutlineVisible;
        private bool hasInitialized;

        /// <summary>Gets whether this door has been permanently opened for the current session.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Gets whether the nearby player is standing on the side from which this door opens.</summary>
        public bool IsPlayerOnWrongSide => accessMode == DoorAccessMode.OneSided
            && playerInteraction != null
            && !IsOnAuthorizedSide(playerInteraction.gameObject);

        /// <summary>Gets a prompt that describes the door state and its access requirement.</summary>
        public override string InteractionPrompt
        {
            get
            {
                if (IsOpen)
                {
                    return OpenPrompt;
                }
                if (!allowDirectInteraction)
                {
                    return LockedPrompt;
                }
                if (accessMode == DoorAccessMode.OneSided)
                {
                    if (IsPlayerOnWrongSide)
                    {
                        return WrongSidePrompt;
                    }

                    string sideLabel = string.IsNullOrWhiteSpace(authorizedSideLabel)
                        ? DefaultSideLabel : authorizedSideLabel;
                    return string.Format(OneSidedPromptFormat, sideLabel);
                }

                string itemName = string.IsNullOrWhiteSpace(requiredItemDisplayName)
                    ? requiredItemId : requiredItemDisplayName;
                bool hasRequiredItem = GameManager.Instance != null &&
                    GameManager.Instance.HasItem(requiredItemId);
                return hasRequiredItem
                    ? string.Format(RequiredItemPromptFormat, itemName)
                    : string.Format(MissingItemPromptFormat, itemName);
            }
        }

        private void Awake()
        {
            GameManager gameManager = GameManager.Instance;
            if (doorStateChannel == null && gameManager != null)
            {
                doorStateChannel = gameManager.DoorStateChannel;
            }
            if (blockingCollider == null)
            {
                blockingCollider = GetComponent<Collider2D>();
            }
            if (accessPlane == null)
            {
                accessPlane = transform;
            }
            if (doorAssetRoot == null)
            {
                doorAssetRoot = gameObject;
            }

            if (!HasValidConfiguration())
            {
                Debug.LogError(string.Format(InvalidSetupErrorFormat, doorIdentifier), this);
                enabled = false;
                return;
            }

            closedRotation = visualHinge != null ? visualHinge.localRotation : Quaternion.identity;
            IsOpen = initiallyOpen;
            hasInitialized = true;
            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }
            InitializeInteractionOutline();
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
            if (shouldOpen)
            {
                doorStateChannel.RequestPermanentDoorOpen(doorIdentifier);
            }
            else
            {
                ApplyDoorState(false, true);
            }
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

            UpdateInteractionOutline();
        }

        private void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
            }
        }

        /// <summary>Requests permanent opening when the configured access requirement is satisfied.</summary>
        public void OpenDoor()
        {
            TryOpenDoor(null);
        }

        /// <summary>Keeps the door closed only before it has been opened; an open door cannot be closed.</summary>
        public void CloseDoor()
        {
            if (!hasInitialized || IsOpen || doorStateChannel.IsDoorPermanentlyOpen(doorIdentifier))
            {
                return;
            }

            doorStateChannel.RequestDoorState(doorIdentifier, false);
        }

        private void InitializeInteractionOutline()
        {
            interactionOutline = GetComponent<LineRenderer>();
            if (interactionOutline == null)
            {
                interactionOutline = gameObject.AddComponent<LineRenderer>();
            }

            interactionOutline.useWorldSpace = true;
            interactionOutline.loop = true;
            interactionOutline.positionCount = OutlinePointCount;
            interactionOutline.startWidth = InteractionOutlineWidth;
            interactionOutline.endWidth = InteractionOutlineWidth;
            interactionOutline.startColor = InteractionOutlineColor;
            interactionOutline.endColor = InteractionOutlineColor;
            interactionOutline.enabled = false;

            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            outlineBoundsRenderer = spriteRenderer;
            if (spriteRenderer != null)
            {
                interactionOutline.sortingLayerID = spriteRenderer.sortingLayerID;
                interactionOutline.sortingOrder = spriteRenderer.sortingOrder + OutlineSortingOrderOffset;
            }
            else
            {
                interactionOutline.sortingOrder = DefaultOutlineSortingOrder;
            }

            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader != null)
            {
                runtimeOutlineMaterial = new Material(outlineShader);
                runtimeOutlineMaterial.color = InteractionOutlineColor;
                interactionOutline.material = runtimeOutlineMaterial;
            }
        }

        private void UpdateInteractionOutline()
        {
            if (interactionOutline == null || blockingCollider == null || IsOpen)
            {
                return;
            }

            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            if (playerInteraction == null)
            {
                SetInteractionOutlineVisible(false);
                return;
            }

            Bounds bounds = outlineBoundsRenderer != null
                ? outlineBoundsRenderer.bounds
                : blockingCollider.bounds;
            Vector3 playerPosition = playerInteraction.transform.position;
            playerPosition.z = bounds.center.z;
            bool shouldShowOutline = bounds.SqrDistance(playerPosition) <=
                InteractionHighlightRadius * InteractionHighlightRadius;
            if (shouldShowOutline)
            {
                float width = bounds.size.x;
                float height = bounds.size.y;
                float minX = bounds.min.x + width * OutlineHorizontalInset;
                float maxX = bounds.max.x - width * OutlineHorizontalInset;
                float minY = bounds.min.y + height * OutlineBottomInset;
                float maxY = bounds.max.y - height * OutlineTopInset;
                Vector3 lowerLeft = new Vector3(minX, minY, bounds.center.z);
                Vector3 upperLeft = new Vector3(minX, maxY, bounds.center.z);
                Vector3 upperRight = new Vector3(maxX, maxY, bounds.center.z);
                Vector3 lowerRight = new Vector3(maxX, minY, bounds.center.z);
                interactionOutline.SetPosition(0, lowerLeft);
                interactionOutline.SetPosition(1, upperLeft);
                interactionOutline.SetPosition(2, upperRight);
                interactionOutline.SetPosition(3, lowerRight);
            }

            SetInteractionOutlineVisible(shouldShowOutline);
        }

        private void SetInteractionOutlineVisible(bool visible)
        {
            if (interactionOutline == null || isInteractionOutlineVisible == visible)
            {
                return;
            }

            isInteractionOutlineVisible = visible;
            interactionOutline.enabled = visible;
        }


        /// <summary>Attempts to open the door after validating the interactor and configured access condition.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor != null)
            {
                TryOpenDoor(interactor);
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

        private bool HasValidConfiguration()
        {
            if (string.IsNullOrWhiteSpace(doorIdentifier) || doorStateChannel == null || blockingCollider == null ||
                !Enum.IsDefined(typeof(DoorAccessMode), accessMode) || doorAssetRoot == null)
            {
                return false;
            }

            if (doorAssetRoot.transform.parent == null ||
                (transform != doorAssetRoot.transform && !transform.IsChildOf(doorAssetRoot.transform)) ||
                (blockingCollider.transform != doorAssetRoot.transform &&
                 !blockingCollider.transform.IsChildOf(doorAssetRoot.transform)) ||
                (visualHinge != null && visualHinge != doorAssetRoot.transform &&
                 !visualHinge.IsChildOf(doorAssetRoot.transform)) ||
                doorAssetRoot.GetComponentsInChildren<DoorController>(true).Length != 1)
            {
                Debug.LogError(InvalidAssetRootError, this);
                return false;
            }

            if (accessMode == DoorAccessMode.OneSided)
            {
                return accessPlane != null && localAccessAxis.sqrMagnitude > Zero;
            }

            return !string.IsNullOrWhiteSpace(requiredItemId);
        }

        private bool TryOpenDoor(GameObject interactor)
        {
            if (!hasInitialized || !isActiveAndEnabled || IsOpen || !allowDirectInteraction ||
                !HasValidConfiguration())
            {
                return false;
            }

            GameManager gameManager = GameManager.Instance;
            if (accessMode == DoorAccessMode.OneSided)
            {
                if (!IsOnAuthorizedSide(interactor))
                {
                    return false;
                }
            }
            else if (gameManager == null || !gameManager.HasItem(requiredItemId))
            {
                return false;
            }

            if (accessMode == DoorAccessMode.RequiredItem &&
                !gameManager.TryConsumeItem(requiredItemId))
            {
                return false;
            }

            doorStateChannel.RequestPermanentDoorOpen(doorIdentifier);
            return true;
        }

        private bool IsOnAuthorizedSide(GameObject interactor)
        {
            if (interactor == null || accessPlane == null || localAccessAxis.sqrMagnitude <= Zero)
            {
                return false;
            }

            Vector3 worldAxis = accessPlane.TransformDirection(localAccessAxis.normalized);
            Vector3 offsetFromPlane = interactor.transform.position - accessPlane.position;
            float sideDot = Vector3.Dot(offsetFromPlane, worldAxis);
            if (Mathf.Abs(sideDot) <= MinimumSideDot)
            {
                return false;
            }

            return (sideDot > Zero) == openFromPositiveSide;
        }

        private void HandleDoorStateRequested(string requestedIdentifier, bool shouldOpen)
        {
            if (!string.Equals(requestedIdentifier, doorIdentifier, StringComparison.Ordinal))
            {
                return;
            }

            if (shouldOpen && !doorStateChannel.IsDoorPermanentlyOpen(doorIdentifier))
            {
                doorStateChannel.RequestPermanentDoorOpen(doorIdentifier);
                return;
            }

            ApplyDoorState(shouldOpen, false);
        }

        private void ApplyDoorState(bool shouldOpen, bool snapVisual)
        {
            if (shouldOpen)
            {
                IsOpen = true;
                blockingCollider.enabled = false;
                targetRotation = closedRotation * Quaternion.Euler(Zero, Zero, openAngleDegrees);
                if (snapVisual && visualHinge != null)
                {
                    visualHinge.localRotation = targetRotation;
                }

                Destroy(doorAssetRoot);
                return;
            }

            IsOpen = false;
            blockingCollider.enabled = true;
            targetRotation = closedRotation;
            if (snapVisual && visualHinge != null)
            {
                visualHinge.localRotation = targetRotation;
            }
        }
    }
}
