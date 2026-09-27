using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.Puzzles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Finds nearby XY interactables with non-allocating Physics2D queries.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const string InteractActionPath = "Player/Interact";
        private const string MissingInputError = "PlayerInteraction requires Player/Interact.";
        private const float DefaultRadius = 0.45f;
        private const float MinimumInteractionRadius = DefaultRadius;
        private const float MinimumDistanceSquared = 0.0001f;
        private const float Zero = 0f;
        private const int Capacity = 64;
        private const int FirstIndex = 0;
        private const int AllLayers = -1;
        private static readonly Color InteractionHighlightColor = new Color(1f, 0.82f, 0.42f, 1f);
        private const float InteractionHighlightBlend = 0.42f;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private LayerMask interactableLayers = AllLayers;
        [SerializeField] private LayerMask lineOfSightObstacleLayers = AllLayers;
        [SerializeField, Min(Zero)] private float interactionRadius = DefaultRadius;
        private readonly Collider2D[] nearbyColliders = new Collider2D[Capacity];
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[Capacity];
        private InputAction interactAction;
        private TopDownCharacterController controller;
        private InteractableBase highlightedInteractable;
        private SpriteRenderer[] highlightedRenderers = System.Array.Empty<SpriteRenderer>();
        private Color[] originalRendererColors = System.Array.Empty<Color>();
        public InteractableBase CurrentInteractable { get; private set; }
        /// <summary>Gets the configured input-action asset used by the player interaction controls.</summary>
        public InputActionAsset InputActions => inputActions;
        private bool AllowsGameplay => gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration;

        private void Awake()
        {
            controller = GetComponent<TopDownCharacterController>();
            DoorController.EnsureHallKeyDoor();
            InputAction source = inputActions != null ? inputActions.FindAction(InteractActionPath) : null;
            if (source == null)
            {
                Debug.LogError(MissingInputError, this);
                enabled = false;
                return;
            }
            interactAction = source.Clone();
        }

        private void OnEnable() { interactAction?.Enable(); }
        private void OnDisable()
        {
            interactAction?.Disable();
            CurrentInteractable = null;
            SetHighlightedInteractable(null);
        }
        private void OnDestroy() { interactAction?.Dispose(); }
        private void FixedUpdate() { FindNearestInteractable(); }

        private void Update()
        {
            if (!AllowsGameplay)
            {
                CurrentInteractable = null;
                SetHighlightedInteractable(null);
                return;
            }
            if (interactAction != null && interactAction.WasPerformedThisFrame())
            {
                FindNearestInteractable();
                if (CurrentInteractable != null && CurrentInteractable.isActiveAndEnabled)
                {
                    CurrentInteractable.Interact(gameObject);
                }
            }
        }



        private void FindNearestInteractable()
        {
            CurrentInteractable = null;
            if (!AllowsGameplay)
            {
                SetHighlightedInteractable(null);
                return;
            }
            Physics2D.SyncTransforms();
            Vector2 origin = transform.position;
            Vector2 facing = controller != null ? controller.FacingDirection : Vector2.down;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(interactableLayers);
            float effectiveInteractionRadius = Mathf.Max(interactionRadius, MinimumInteractionRadius);
            float searchRadius = Mathf.Max(
                effectiveInteractionRadius,
                Mathf.Max(
                    PushableBox.InteractionRadius,
                    Mathf.Max(
                        PuzzleAssetInteractable.MaximumInteractionRadius,
                        Mathf.Max(
                            ColorPuzzlePortalInteractable.MaximumInteractionRadius,
                            ChessPortalInteractable.MaximumInteractionRadius))));
            int count = Physics2D.OverlapCircle(origin, searchRadius, filter, nearbyColliders);
            float nearestDistanceSquared = float.PositiveInfinity;
            bool nearestIsPortal = false;
            for (int index = FirstIndex; index < count; index++)
            {
                Collider2D collider = nearbyColliders[index];
                if (collider.transform.IsChildOf(transform)) continue;
                InteractableBase candidate = collider.GetComponentInParent<InteractableBase>();
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                Vector2 point = collider.ClosestPoint(origin);
                Vector2 offset = point - origin;
                float candidateRadius = effectiveInteractionRadius;
                bool isPortal = false;
                if (candidate is ColorPuzzlePortalInteractable colorPortal)
                {
                    isPortal = true;
                    point = colorPortal.InteractionPoint;
                    offset = point - origin;
                    candidateRadius = colorPortal.InteractionRadius;
                }
                else if (candidate is ChessPortalInteractable chessPortal)
                {
                    isPortal = true;
                    point = chessPortal.InteractionPoint;
                    offset = point - origin;
                    candidateRadius = chessPortal.InteractionRadius;
                }
                else if (candidate is PuzzleAssetInteractable puzzleAsset)
                {
                    isPortal = true;
                    point = puzzleAsset.InteractionPoint;
                    offset = point - origin;
                    candidateRadius = puzzleAsset.InteractionRadius;
                }
                else if (candidate is PushableBox pushableBox)
                {
                    candidateRadius = Mathf.Max(candidateRadius, PushableBox.InteractionRadius);
                    BoxDragController dragController = GetComponent<BoxDragController>();
                    if (dragController != null)
                    {
                        if (dragController.IsGrabbing)
                        {
                            if (dragController.GrabbedBox != pushableBox) continue;
                        }
                        else if (!dragController.CanGrab(pushableBox))
                        {
                            continue;
                        }
                    }
                }
                else if (candidate is DoorController door)
                {
                    candidateRadius = door.InteractionRadius;
                }

                float candidateDistanceSquared = offset.sqrMagnitude;
                if (candidateDistanceSquared >= candidateRadius * candidateRadius
                    || (!isPortal && Vector2.Dot(facing, offset) < Zero)
                    || (nearestIsPortal && !isPortal)
                    || (nearestIsPortal == isPortal && candidateDistanceSquared >= nearestDistanceSquared))
                {
                    continue;
                }

                if (!isPortal
                    && point != origin
                    && candidateDistanceSquared > MinimumDistanceSquared
                    && !HasLineOfSight(origin, point, candidate, candidate is PushableBox))
                {
                    continue;
                }

                nearestDistanceSquared = candidateDistanceSquared;
                nearestIsPortal = isPortal;
                CurrentInteractable = candidate;
            }
            SetHighlightedInteractable(CurrentInteractable);
        }

        private void SetHighlightedInteractable(InteractableBase interactable)
        {
            if (highlightedInteractable == interactable) return;

            if (highlightedInteractable is IInteractionHighlightTarget previousHighlightTarget)
            {
                previousHighlightTarget.SetInteractionHighlighted(false);
            }

            for (int index = 0; index < highlightedRenderers.Length; index++)
            {
                SpriteRenderer renderer = highlightedRenderers[index];
                if (renderer != null) renderer.color = originalRendererColors[index];
            }

            highlightedInteractable = interactable;
            if (interactable == null)
            {
                highlightedRenderers = System.Array.Empty<SpriteRenderer>();
                originalRendererColors = System.Array.Empty<Color>();
                return;
            }

            if (interactable is IInteractionHighlightTarget nextHighlightTarget)
            {
                nextHighlightTarget.SetInteractionHighlighted(true);
            }

            highlightedRenderers = interactable.GetComponentsInChildren<SpriteRenderer>(true);
            originalRendererColors = new Color[highlightedRenderers.Length];
            for (int index = 0; index < highlightedRenderers.Length; index++)
            {
                SpriteRenderer renderer = highlightedRenderers[index];
                if (renderer == null) continue;
                Color originalColor = renderer.color;
                originalRendererColors[index] = originalColor;
                Color targetColor = new Color(InteractionHighlightColor.r, InteractionHighlightColor.g,
                    InteractionHighlightColor.b, originalColor.a);
                renderer.color = Color.Lerp(originalColor, targetColor, InteractionHighlightBlend);
            }
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 target, InteractableBase candidate, bool ignorePuzzleObstacles)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(lineOfSightObstacleLayers);
            int count = Physics2D.Linecast(origin, target, filter, sightHits);
            if (count == sightHits.Length) return false;
            PuzzleGrid candidateGrid = ignorePuzzleObstacles ? candidate.GetComponentInParent<PuzzleGrid>() : null;
            for (int index = FirstIndex; index < count; index++)
            {
                Transform hit = sightHits[index].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(candidate.transform)) continue;
                if (candidateGrid != null && hit.GetComponentInParent<PuzzleGrid>() == candidateGrid) continue;
                return false;
            }
            return true;
        }
    }
}
