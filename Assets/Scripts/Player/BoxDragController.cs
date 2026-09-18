using System.Collections;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Puzzles;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Player
{
    /// <summary>Grabs an adjacent <see cref="PushableBox"/> and pushes or pulls it one cell at a time along the facing axis.</summary>
    [RequireComponent(typeof(TopDownCharacterController), typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class BoxDragController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string MissingInputError = "BoxDragController requires a Player/Move action.";
        private const string MissingGridWarning = "BoxDragController could not find a PuzzleGrid; box dragging is disabled.";
        private const float DefaultAxisThreshold = 0.5f;
        private const float MinimumAxisThreshold = 0.05f;
        private const float MaximumAxisThreshold = 1f;
        private const float MinimumStepDuration = 0.01f;
        private const float Zero = 0f;
        private const int CardinalManhattanLength = 1;
        private static readonly WaitForFixedUpdate WaitForPhysicsStep = new WaitForFixedUpdate();

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private PuzzleGrid grid;
        [SerializeField, Range(MinimumAxisThreshold, MaximumAxisThreshold)] private float axisThreshold = DefaultAxisThreshold;
        private Rigidbody2D body;
        private BoxCollider2D playerCollider;
        private TopDownCharacterController controller;
        private InputAction moveAction;
        private Coroutine stepRoutine;
        private Vector2Int playerCell;
        private Vector2Int dragAxis;
        private Vector2Int stepTargetCell;

        public PushableBox GrabbedBox { get; private set; }
        public bool IsGrabbing => GrabbedBox != null;
        public bool IsStepping => stepRoutine != null;
        private bool AllowsGameplay => gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<BoxCollider2D>();
            controller = GetComponent<TopDownCharacterController>();
            InputAction sourceMove = inputActions != null ? inputActions.FindAction(MoveActionPath) : null;
            if (sourceMove == null)
            {
                Debug.LogError(MissingInputError, this);
                enabled = false;
                return;
            }
            moveAction = sourceMove.Clone();
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            if (grid == null) Debug.LogWarning(MissingGridWarning, this);
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
            Release();
        }

        private void OnDestroy() { moveAction?.Dispose(); }

        private void Update()
        {
            if (!IsGrabbing) return;
            if (!GrabbedBox.isActiveAndEnabled) { Release(); return; }
            if (!AllowsGameplay || IsStepping || GrabbedBox.IsMoving || moveAction == null) return;
            float axisInput = Vector2.Dot(moveAction.ReadValue<Vector2>(), dragAxis);
            if (axisInput >= axisThreshold) TryStep(dragAxis);
            else if (axisInput <= -axisThreshold) TryStep(-dragAxis);
        }

        /// <summary>Releases the current box when one is held; otherwise tries to grab the supplied box. Returns the resulting grab state.</summary>
        public bool ToggleGrab(PushableBox box)
        {
            if (IsStepping) return IsGrabbing;
            if (IsGrabbing)
            {
                Release();
                return false;
            }
            return TryGrab(box);
        }

        /// <summary>Grabs the box when it occupies a cardinal neighbour cell; snaps the player to its cell center and locks free movement.</summary>
        public bool TryGrab(PushableBox box)
        {
            if (box == null || grid == null || !box.isActiveAndEnabled || box.IsMoving || IsGrabbing) return false;
            Vector2Int currentCell = grid.WorldToCell(body.position);
            Vector2Int delta = box.Cell - currentCell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != CardinalManhattanLength) return false;
            playerCell = currentCell;
            dragAxis = delta;
            GrabbedBox = box;
            box.IsGrabbed = true;
            controller.SetMovementEnabled(false);
            body.position = grid.CellToWorld(playerCell);
            return true;
        }

        /// <summary>Drops the held box (if any), finishing any step in progress by snapping to its target cell, and restores free movement.</summary>
        public void Release()
        {
            if (stepRoutine != null)
            {
                StopCoroutine(stepRoutine);
                stepRoutine = null;
                if (grid != null && body != null) body.position = grid.CellToWorld(stepTargetCell);
                playerCell = stepTargetCell;
            }
            if (GrabbedBox != null)
            {
                GrabbedBox.IsGrabbed = false;
                GrabbedBox = null;
            }
            if (controller != null) controller.SetMovementEnabled(true);
        }

        private void TryStep(Vector2Int direction)
        {
            bool isPush = direction == dragAxis;
            bool canStep = isPush
                ? GrabbedBox.CanMove(direction, playerCollider)
                : grid.IsCellFree(playerCell + direction, playerCollider, GrabbedBox.Collider);
            if (canStep) stepRoutine = StartCoroutine(StepRoutine(direction));
        }

        private IEnumerator StepRoutine(Vector2Int direction)
        {
            PushableBox box = GrabbedBox;
            stepTargetCell = playerCell + direction;
            Vector2 start = body.position;
            Vector2 target = grid.CellToWorld(stepTargetCell);
            float duration = Mathf.Max(MinimumStepDuration, box.StepDuration);
            float elapsed = Zero;
            box.Move(direction);
            while (elapsed < duration)
            {
                yield return WaitForPhysicsStep;
                elapsed += Time.fixedDeltaTime;
                body.MovePosition(Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
            }
            body.position = target;
            playerCell = stepTargetCell;
            stepRoutine = null;
        }

        private void HandleTimelineChanged(TimelineEra era) { Release(); }
    }
}
