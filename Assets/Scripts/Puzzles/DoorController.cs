using System;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>A permanently opening, channel-driven door with an independently animated visual hinge.</summary>
    [DisallowMultipleComponent]
    public sealed class DoorController : InteractableBase, ITimelineObstacle, IProximityDescription
    {
        private const string DefaultDoorIdentifier = "HallDoor";
        private const string OpenPrompt = "Door open";
        private const string LockedPrompt = "Locked - use the lever";
        private const string OneSidedPromptFormat = "One-way door: only opens from {0}";
        private const string RequiredItemPromptFormat = "Use {0} to open the door";
        private const string MissingItemPromptFormat = "Locked: requires {0}";
        private const string WrongSidePrompt = "Does not open from this side";
        private const string DiningDoorAuthorizedPrompt = "This door can be opened from this side.";
        private const string DefaultSideLabel = "the authorized side";
        private const string InvalidSetupErrorFormat = "DoorController '{0}' requires an identifier, channel, blocking collider, valid access configuration, and dedicated door asset root.";
        private const string InvalidAssetRootError = "DoorController asset root must be a dedicated non-scene-root object containing this door and its blocking collider, and must not contain another DoorController.";
        private const float DefaultOpenAngle = -100f;
        private const float DefaultAnimationSpeed = 180f;
        private const float Zero = 0f;
        private const float MinimumSideDot = 0.0001f;
        private const string OutlineShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string DeepDoorObjectName = "DoorDeep";
        private const string DiningDoorObjectName = "DoorDinner";
        private const string HallKeyDoorObjectName = "DoorH";
        private const string HallKeyDoorIdentifier = "DoorH";
        private const string HallKeyDoorLockedPrompt = "This door is locked.";
        private const string HallKeyDoorItemDisplayName = "the chess puzzle key";
        private const string FireDoorObjectName = "DoorB";
        private const string FireDoorSoundId = "present-fire-door";
        private const float HallKeyDoorInteractionRadius = 0.35f;
        public const float DoorInteractionRadius = 0.25f;
        private const float InteractionOutlineWidth = 0.02f;
        private const float DoubleDoorHorizontalInset = 0.04f;
        private const float LowerDoorMinYFromBottom = 0.02f;
        private const float LowerDoorMaxYFromBottom = 0.58f;
        private const float UpperDoorMinYFromBottom = 0.76f;
        private const float UpperDoorMaxYFromBottom = 0.95f;
        private const int DefaultOutlineSortingOrder = 21;
        private const int OutlineSortingOrderOffset = 1;
        private const int OutlinePointCount = 4;
        private const string SceneGuideLabelSuffix = " [texto puerta]";
        private static readonly Color InteractionOutlineColor = Color.white;
        private static readonly Color SceneGuideColor = new Color(1f, 0.65f, 0.2f, 0.9f);

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
        [SerializeField, TextArea] private string proximityDescription;
        [SerializeField] private bool showProximityDescriptionOnWrongSideOnly;

        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private LineRenderer interactionOutline;
        private LineRenderer secondaryInteractionOutline;
        private SpriteRenderer outlineBoundsRenderer;
        private Material runtimeOutlineMaterial;
        private bool hasInitialized;
        private bool wasFireDoorSelected;

        /// <summary>Gets whether this door has been permanently opened for the current session.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Gets the radius used for this door's nearby interaction and outline checks.</summary>
        public float InteractionRadius => !string.IsNullOrWhiteSpace(proximityDescription)
            ? InteractableBase.ProximityTextInteractionRadius
            : string.Equals(gameObject.name, HallKeyDoorObjectName, StringComparison.Ordinal)
                ? HallKeyDoorInteractionRadius : DoorInteractionRadius;

        private float InteractionHighlightRadius => InteractionRadius;

        /// <summary>Gets an optional narrative description shown while the player is near this door.</summary>
        public string ProximityDescription
        {
            get
            {
                if (showProximityDescriptionOnWrongSideOnly && !IsPlayerOnWrongSide)
                {
                    return string.Empty;
                }

                if (string.Equals(gameObject.name, HallKeyDoorObjectName, StringComparison.Ordinal)
                    && (IsOpen || (GameManager.Instance != null && GameManager.Instance.HasItem(requiredItemId))))
                {
                    return string.Empty;
                }

                return proximityDescription;
            }
        }

        /// <summary>Gets whether the nearby player is standing on the side from which this door opens.</summary>
        public bool IsPlayerOnWrongSide => accessMode == DoorAccessMode.OneSided
            && playerInteraction != null
            && !IsOnAuthorizedSide(playerInteraction.gameObject);

        /// <summary>Gets whether the interaction panel should prepend its usual interaction key to this door's status text.</summary>
        public bool ShouldShowInteractionPrefix
        {
            get
            {
                if (IsOpen) return true;
                if (string.Equals(gameObject.name, DiningDoorObjectName, StringComparison.Ordinal)
                    && accessMode == DoorAccessMode.OneSided && !IsPlayerOnWrongSide)
                {
                    return false;
                }
                if (string.Equals(gameObject.name, HallKeyDoorObjectName, StringComparison.Ordinal)
                    && accessMode == DoorAccessMode.RequiredItem
                    && (GameManager.Instance == null || !GameManager.Instance.HasItem(requiredItemId)))
                {
                    return false;
                }
                return true;
            }
        }

        private bool UsesDoubleDoorInteractionOutline =>
            string.Equals(gameObject.name, DeepDoorObjectName, StringComparison.Ordinal)
            || string.Equals(gameObject.name, DiningDoorObjectName, StringComparison.Ordinal);

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

                    if (string.Equals(gameObject.name, DiningDoorObjectName, StringComparison.Ordinal)
                        || string.Equals(gameObject.name, DeepDoorObjectName, StringComparison.Ordinal))
                    {
                        return DiningDoorAuthorizedPrompt;
                    }

                    string sideLabel = string.IsNullOrWhiteSpace(authorizedSideLabel)
                        ? DefaultSideLabel : authorizedSideLabel;
                    return string.Format(OneSidedPromptFormat, sideLabel);
                }

                string itemName = string.IsNullOrWhiteSpace(requiredItemDisplayName)
                    ? requiredItemId : requiredItemDisplayName;
                bool hasRequiredItem = GameManager.Instance != null &&
                    GameManager.Instance.HasItem(requiredItemId);
                if (!hasRequiredItem && string.Equals(gameObject.name, HallKeyDoorObjectName, StringComparison.Ordinal))
                {
                    return HallKeyDoorLockedPrompt;
                }
                return hasRequiredItem
                    ? string.Format(RequiredItemPromptFormat, itemName)
                    : string.Format(MissingItemPromptFormat, itemName);
            }
        }

        /// <summary>Ensures Hall's locked DoorH has a 2D blocking collider and the normal door interaction behavior.</summary>
        public static void EnsureHallKeyDoor()
        {
            GameObject keyDoor = GameObject.Find(HallKeyDoorObjectName);
            if (keyDoor == null)
            {
                return;
            }

            Collider2D blockingCollider = keyDoor.GetComponent<Collider2D>();
            if (blockingCollider == null)
            {
                blockingCollider = keyDoor.AddComponent<BoxCollider2D>();
            }
            if (blockingCollider is BoxCollider2D boxCollider)
            {
                FitColliderToSprite(keyDoor.transform, boxCollider);
            }
            blockingCollider.enabled = true;
            blockingCollider.isTrigger = false;

            if (keyDoor.GetComponent<DoorController>() == null)
            {
                keyDoor.AddComponent<DoorController>();
            }
        }

        private static void FitColliderToSprite(Transform doorTransform, BoxCollider2D boxCollider)
        {
            SpriteRenderer spriteRenderer = doorTransform.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds spriteBounds = spriteRenderer.sprite.bounds;
                Vector3 firstCorner = doorTransform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(
                    new Vector3(spriteBounds.min.x, spriteBounds.min.y, Zero)));
                Vector3 secondCorner = doorTransform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(
                    new Vector3(spriteBounds.max.x, spriteBounds.max.y, Zero)));
                Vector3 thirdCorner = doorTransform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(
                    new Vector3(spriteBounds.min.x, spriteBounds.max.y, Zero)));
                Vector3 fourthCorner = doorTransform.InverseTransformPoint(spriteRenderer.transform.TransformPoint(
                    new Vector3(spriteBounds.max.x, spriteBounds.min.y, Zero)));
                SetBoxColliderBounds(boxCollider, firstCorner, secondCorner, thirdCorner, fourthCorner);
                return;
            }

            Renderer doorRenderer = doorTransform.GetComponentInChildren<Renderer>(true);
            if (doorRenderer == null)
            {
                return;
            }

            Bounds rendererBounds = doorRenderer.bounds;
            Vector3 minimum = rendererBounds.min;
            Vector3 maximum = rendererBounds.max;
            Vector3 firstWorldCorner = doorTransform.InverseTransformPoint(new Vector3(minimum.x, minimum.y, Zero));
            Vector3 secondWorldCorner = doorTransform.InverseTransformPoint(new Vector3(maximum.x, maximum.y, Zero));
            Vector3 thirdWorldCorner = doorTransform.InverseTransformPoint(new Vector3(minimum.x, maximum.y, Zero));
            Vector3 fourthWorldCorner = doorTransform.InverseTransformPoint(new Vector3(maximum.x, minimum.y, Zero));
            SetBoxColliderBounds(boxCollider, firstWorldCorner, secondWorldCorner, thirdWorldCorner, fourthWorldCorner);
        }

        private static void SetBoxColliderBounds(BoxCollider2D boxCollider, Vector3 firstCorner,
            Vector3 secondCorner, Vector3 thirdCorner, Vector3 fourthCorner)
        {
            float minX = Mathf.Min(firstCorner.x, secondCorner.x, thirdCorner.x, fourthCorner.x);
            float maxX = Mathf.Max(firstCorner.x, secondCorner.x, thirdCorner.x, fourthCorner.x);
            float minY = Mathf.Min(firstCorner.y, secondCorner.y, thirdCorner.y, fourthCorner.y);
            float maxY = Mathf.Max(firstCorner.y, secondCorner.y, thirdCorner.y, fourthCorner.y);
            boxCollider.offset = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            boxCollider.size = new Vector2(maxX - minX, maxY - minY);
        }

        private void Awake()
        {
            if (string.Equals(gameObject.name, HallKeyDoorObjectName, StringComparison.Ordinal))
            {
                doorIdentifier = HallKeyDoorIdentifier;
                accessMode = DoorAccessMode.RequiredItem;
                requiredItemId = PuzzleItemIds.KitchenLeftDoorKey;
                requiredItemDisplayName = HallKeyDoorItemDisplayName;
            }
            else if (string.Equals(gameObject.name, DeepDoorObjectName, StringComparison.Ordinal)
                && string.Equals(doorIdentifier, DiningDoorObjectName, StringComparison.Ordinal))
            {
                doorIdentifier = DeepDoorObjectName;
            }

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
            if (wasFireDoorSelected)
            {
                SoundManager.StopLoop(FireDoorSoundId);
                wasFireDoorSelected = false;
            }
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
            bool isFireDoorSelected = string.Equals(gameObject.name, FireDoorObjectName, StringComparison.Ordinal)
                && playerInteraction != null
                && playerInteraction.CurrentInteractable == this;
            if (isFireDoorSelected && !wasFireDoorSelected)
            {
                SoundManager.PlayLoop(FireDoorSoundId);
            }
            else if (!isFireDoorSelected && wasFireDoorSelected)
            {
                SoundManager.StopLoop(FireDoorSoundId);
            }
            wasFireDoorSelected = isFireDoorSelected;
        }

        private void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Camera sceneCamera = Camera.current;
            if (sceneCamera == null || sceneCamera.cameraType != CameraType.SceneView
                || string.IsNullOrWhiteSpace(proximityDescription))
            {
                return;
            }

            Collider2D textArea = blockingCollider != null ? blockingCollider : GetComponent<Collider2D>();
            if (textArea == null)
            {
                return;
            }

            Color previousGizmoColor = Gizmos.color;
            Color previousHandleColor = Handles.color;
            Gizmos.color = SceneGuideColor;
            Handles.color = SceneGuideColor;
            BoxCollider2D boxArea = textArea as BoxCollider2D;
            if (boxArea != null)
            {
                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = boxArea.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(boxArea.offset, boxArea.size);
                Gizmos.matrix = previousMatrix;
                Handles.Label(boxArea.transform.TransformPoint(boxArea.offset),
                    gameObject.name + SceneGuideLabelSuffix);
            }
            else
            {
                Bounds areaBounds = textArea.bounds;
                Gizmos.DrawWireCube(areaBounds.center, areaBounds.size);
                Handles.Label(areaBounds.center, gameObject.name + SceneGuideLabelSuffix);
            }

            Gizmos.color = previousGizmoColor;
            Handles.color = previousHandleColor;
        }
#endif

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

            outlineBoundsRenderer = GetComponentInChildren<SpriteRenderer>(true);
            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader != null)
            {
                runtimeOutlineMaterial = new Material(outlineShader);
                runtimeOutlineMaterial.color = InteractionOutlineColor;
            }

            ConfigureInteractionOutline(interactionOutline);
            if (UsesDoubleDoorInteractionOutline)
            {
                GameObject secondaryOutlineObject = new GameObject("SecondaryDoorInteractionOutline");
                secondaryOutlineObject.transform.SetParent(transform, false);
                secondaryInteractionOutline = secondaryOutlineObject.AddComponent<LineRenderer>();
                ConfigureInteractionOutline(secondaryInteractionOutline);
            }
        }

        private void ConfigureInteractionOutline(LineRenderer outline)
        {
            outline.useWorldSpace = true;
            outline.loop = true;
            outline.positionCount = OutlinePointCount;
            outline.startWidth = InteractionOutlineWidth;
            outline.endWidth = InteractionOutlineWidth;
            outline.startColor = InteractionOutlineColor;
            outline.endColor = InteractionOutlineColor;
            if (runtimeOutlineMaterial != null) outline.material = runtimeOutlineMaterial;
            outline.enabled = false;

            if (outlineBoundsRenderer != null)
            {
                outline.sortingLayerID = outlineBoundsRenderer.sortingLayerID;
                outline.sortingOrder = outlineBoundsRenderer.sortingOrder + OutlineSortingOrderOffset;
            }
            else
            {
                outline.sortingOrder = DefaultOutlineSortingOrder;
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
                if (UsesDoubleDoorInteractionOutline)
                {
                    float width = bounds.size.x;
                    float height = bounds.size.y;
                    float minX = bounds.min.x + width * DoubleDoorHorizontalInset;
                    float maxX = bounds.max.x - width * DoubleDoorHorizontalInset;
                    float lowerDoorBottom = bounds.min.y + height * LowerDoorMinYFromBottom;
                    float lowerDoorTop = bounds.min.y + height * LowerDoorMaxYFromBottom;
                    float upperDoorBottom = bounds.min.y + height * UpperDoorMinYFromBottom;
                    float upperDoorTop = bounds.min.y + height * UpperDoorMaxYFromBottom;

                    SetOutlineRectangle(interactionOutline, bounds, minX, maxX,
                        lowerDoorBottom, lowerDoorTop);
                    SetOutlineRectangle(secondaryInteractionOutline, bounds, minX, maxX,
                        upperDoorBottom, upperDoorTop);
                }
                else
                {
                    SetOutlineRectangle(interactionOutline, bounds, bounds.min.x, bounds.max.x,
                        bounds.min.y, bounds.max.y);
                }
            }

            SetInteractionOutlineVisible(shouldShowOutline);
        }

        private static void SetOutlineRectangle(LineRenderer outline, Bounds bounds,
            float minX, float maxX, float minY, float maxY)
        {
            if (outline == null) return;

            outline.SetPosition(0, new Vector3(minX, minY, bounds.center.z));
            outline.SetPosition(1, new Vector3(minX, maxY, bounds.center.z));
            outline.SetPosition(2, new Vector3(maxX, maxY, bounds.center.z));
            outline.SetPosition(3, new Vector3(maxX, minY, bounds.center.z));
        }

        private void SetInteractionOutlineVisible(bool visible)
        {
            if (interactionOutline != null) interactionOutline.enabled = false;
            if (secondaryInteractionOutline != null) secondaryInteractionOutline.enabled = false;
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
