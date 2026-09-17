using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Player;
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
        private const float MinimumDistanceSquared = 0.0001f;
        private const float Zero = 0f;
        private const int Capacity = 64;
        private const int FirstIndex = 0;
        private const int AllLayers = -1;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private LayerMask interactableLayers = AllLayers;
        [SerializeField] private LayerMask lineOfSightObstacleLayers = AllLayers;
        [SerializeField, Min(Zero)] private float interactionRadius = DefaultRadius;
        private readonly Collider2D[] nearbyColliders = new Collider2D[Capacity];
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[Capacity];
        private InputAction interactAction;
        private TopDownCharacterController controller;
        public InteractableBase CurrentInteractable { get; private set; }
        private bool AllowsGameplay => gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration;

        private void Awake()
        {
            controller = GetComponent<TopDownCharacterController>();
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
        private void OnDisable() { interactAction?.Disable(); CurrentInteractable = null; }
        private void OnDestroy() { interactAction?.Dispose(); }
        private void FixedUpdate() { FindNearestInteractable(); }

        private void Update()
        {
            if (!AllowsGameplay) { CurrentInteractable = null; return; }
            if (interactAction != null && interactAction.WasPerformedThisFrame())
            {
                FindNearestInteractable();
                if (CurrentInteractable != null && CurrentInteractable.isActiveAndEnabled)
                    CurrentInteractable.Interact(gameObject);
            }
        }

        private void FindNearestInteractable()
        {
            CurrentInteractable = null;
            if (!AllowsGameplay) return;
            Physics2D.SyncTransforms();
            Vector2 origin = transform.position;
            Vector2 facing = controller != null ? controller.FacingDirection : Vector2.down;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(interactableLayers);
            int count = Physics2D.OverlapCircle(origin, interactionRadius, filter, nearbyColliders);
            float nearestDistanceSquared = float.PositiveInfinity;
            for (int index = FirstIndex; index < count; index++)
            {
                Collider2D collider = nearbyColliders[index];
                if (collider.transform.IsChildOf(transform)) continue;
                InteractableBase candidate = collider.GetComponentInParent<InteractableBase>();
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                Vector2 point = collider.ClosestPoint(origin);
                Vector2 offset = point - origin;
                if (Vector2.Dot(facing, offset) < Zero || offset.sqrMagnitude >= nearestDistanceSquared) continue;
                if (offset.sqrMagnitude > MinimumDistanceSquared && !HasLineOfSight(origin, point, candidate)) continue;
                nearestDistanceSquared = offset.sqrMagnitude;
                CurrentInteractable = candidate;
            }
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 target, InteractableBase candidate)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(lineOfSightObstacleLayers);
            int count = Physics2D.Linecast(origin, target, filter, sightHits);
            if (count == sightHits.Length) return false;
            for (int index = FirstIndex; index < count; index++)
            {
                Transform hit = sightHits[index].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(candidate.transform)) return false;
            }
            return true;
        }
    }
}
