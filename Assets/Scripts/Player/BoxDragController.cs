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
        private const float GrabAlignmentDuration = 0.1f;
        private const float GrabAssistDistance = 0.18f;
        private const float InputBufferDuration = 0.30f;
        private const float BufferedInputGrace = 0.08f;
        private const float InitialRepeatDelay = 0.22f;
        private const float RepeatInterval = 0.08f;
        private const float BlockedRepeatDelay = 0.12f;
        private const float InputReleaseThreshold = 0.25f;
        private const float MinimumGrabDirectionSquared = 0.0001f;
        private const float Zero = 0f;
        private const float One = 1f;
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
        private PlayerAnimationController animationController;
        private InputAction moveAction;
        private Coroutine stepRoutine;
        private PuzzleExitZone puzzleExitZone;
        private Vector2Int playerCell;
        private Vector2Int dragAxis;
        private Vector2Int stepTargetCell;
        private Vector2Int bufferedDirection;
        private Vector2Int lastInputDirection;
        private float bufferedDirectionExpiresAt;
        private float nextStepAllowedAt;
        private int consecutiveSteps;

        public PushableBox GrabbedBox { get; private set; }
        public bool IsGrabbing => GrabbedBox != null;
        public bool IsStepping => stepRoutine != null;
        private bool IsPuzzleSolved => puzzleExitZone != null && puzzleExitZone.IsSolved;
        private bool AllowsGameplay => !IsPuzzleSolved
            && (gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<BoxCollider2D>();
            controller = GetComponent<TopDownCharacterController>();
            animationController = GetComponent<PlayerAnimationController>();
            puzzleExitZone = FindAnyObjectByType<PuzzleExitZone>();
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
            if (IsPuzzleSolved) { Release(); return; }
            if (!GrabbedBox.isActiveAndEnabled) { Release(); return; }
            if (!AllowsGameplay || moveAction == null)
            {
                ResetBufferedInput();
                return;
            }

            float axisInput = Vector2.Dot(moveAction.ReadValue<Vector2>(), dragAxis);
            Vector2Int requestedDirection = GetRequestedDirection(axisInput);
            float currentTime = Time.unscaledTime;
            if (requestedDirection == Vector2Int.zero)
            {
                lastInputDirection = Vector2Int.zero;
                consecutiveSteps = 0;
                if (currentTime > bufferedDirectionExpiresAt) bufferedDirection = Vector2Int.zero;
            }
            else if (requestedDirection != lastInputDirection)
            {
                lastInputDirection = requestedDirection;
                consecutiveSteps = 0;
                nextStepAllowedAt = currentTime;
                bufferedDirection = requestedDirection;
                bufferedDirectionExpiresAt = currentTime + Mathf.Max(InputBufferDuration, GrabbedBox.StepDuration + BufferedInputGrace);
            }

            if (IsStepping || GrabbedBox.IsMoving || currentTime < nextStepAllowedAt) return;
            Vector2Int direction = bufferedDirection != Vector2Int.zero && currentTime <= bufferedDirectionExpiresAt
                ? bufferedDirection
                : requestedDirection;
            if (direction == Vector2Int.zero) return;
            bufferedDirection = Vector2Int.zero;
            if (TryStep(direction))
            {
                consecutiveSteps++;
                nextStepAllowedAt = currentTime + (consecutiveSteps == 1 ? InitialRepeatDelay : RepeatInterval);
            }
            else
            {
                nextStepAllowedAt = currentTime + BlockedRepeatDelay;
            }
        }

        private Vector2Int GetRequestedDirection(float axisInput)
        {
            if (lastInputDirection != Vector2Int.zero
                && Mathf.Sign(axisInput) == Mathf.Sign(Vector2.Dot(lastInputDirection, dragAxis))
                && Mathf.Abs(axisInput) >= InputReleaseThreshold)
            {
                return lastInputDirection;
            }
            if (axisInput >= axisThreshold) return dragAxis;
            if (axisInput <= -axisThreshold) return -dragAxis;
            return Vector2Int.zero;
        }

        private void ResetBufferedInput()
        {
            bufferedDirection = Vector2Int.zero;
            lastInputDirection = Vector2Int.zero;
            bufferedDirectionExpiresAt = Zero;
            nextStepAllowedAt = Zero;
            consecutiveSteps = 0;
        }

        /// <summary>Releases the current box when one is held; otherwise tries to grab the supplied box. Returns the resulting grab state.</summary>
        public bool ToggleGrab(PushableBox box)
        {
            if (IsPuzzleSolved)
            {
                Release();
                return false;
            }
            if (IsStepping) return IsGrabbing;
            if (IsGrabbing)
            {
                Release();
                return false;
            }
            return TryGrab(box);
        }

        /// <summary>Returns true when a stationary box can be grabbed from an adjacent cell or a valid close-contact position.</summary>
        public bool CanGrab(PushableBox box)
        {
            return !IsPuzzleSolved
                && box != null
                && grid != null
                && body != null
                && box.isActiveAndEnabled
                && !box.IsMoving
                && !IsGrabbing
                && !IsStepping
                && TryGetGrabPlan(box, out _, out _);
        }

        private bool TryGetGrabPlan(PushableBox box, out Vector2Int targetPlayerCell, out Vector2Int targetDragAxis)
        {
            targetPlayerCell = Vector2Int.zero;
            targetDragAxis = Vector2Int.zero;
            if (box.Collider == null) return false;

            Vector2Int boxCell = box.Cell;
            Vector2Int currentPlayerCell = grid.WorldToCell(body.position);
            Vector2Int delta = boxCell - currentPlayerCell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == CardinalManhattanLength)
            {
                targetPlayerCell = currentPlayerCell;
                targetDragAxis = delta;
            }
            else
            {
                Vector2 closestPointOffset = box.Collider.ClosestPoint(body.position) - body.position;
                if (closestPointOffset.sqrMagnitude > GrabAssistDistance * GrabAssistDistance) return false;

                Vector2 directionToBox = (Vector2)box.transform.position - body.position;
                if (directionToBox.sqrMagnitude <= MinimumGrabDirectionSquared && controller != null)
                {
                    directionToBox = controller.FacingDirection;
                }
                targetDragAxis = Mathf.Abs(directionToBox.x) > Mathf.Abs(directionToBox.y)
                    ? (directionToBox.x >= Zero ? Vector2Int.right : Vector2Int.left)
                    : (directionToBox.y >= Zero ? Vector2Int.up : Vector2Int.down);
                targetPlayerCell = boxCell - targetDragAxis;
            }

            if (!grid.IsInside(targetPlayerCell)
                || !grid.IsCellFree(targetPlayerCell, playerCollider, box.Collider))
            {
                return false;
            }

            bool canPush = box.CanMove(targetDragAxis, playerCollider);
            bool canPull = grid.IsCellFree(targetPlayerCell - targetDragAxis, playerCollider, box.Collider);
            return canPush || canPull;
        }

        public bool TryGrab(PushableBox box)
        {
            if (!CanGrab(box) || !TryGetGrabPlan(box, out Vector2Int targetPlayerCell, out Vector2Int targetDragAxis)) return false;
            playerCell = targetPlayerCell;
            dragAxis = targetDragAxis;
            controller.SetFacingDirection(targetDragAxis);
            animationController?.SetFacingDirection(targetDragAxis);
            GrabbedBox = box;
            box.IsGrabbed = true;
            SetGrabbedBoxCollisionIgnored(true);
            controller.SetMovementEnabled(false);
            stepTargetCell = playerCell;
            stepRoutine = StartCoroutine(AlignPlayerToCell());
            return true;
        }

        /// <summary>Smoothly snaps the player's rigidbody (and its collider, which moves rigidly with it) onto the grabbed cell, guaranteeing a consistent one-cell gap to the box before pushing/pulling starts.</summary>
        private IEnumerator AlignPlayerToCell()
        {
            Vector2 start = body.position;
            Vector2 target = grid.CellToWorld(playerCell);
            float elapsed = Zero;
            while (elapsed < GrabAlignmentDuration)
            {
                yield return WaitForPhysicsStep;
                elapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(elapsed / GrabAlignmentDuration);
                float easedProgress = Mathf.SmoothStep(Zero, One, progress);
                body.MovePosition(Vector2.Lerp(start, target, easedProgress));
            }
            body.position = target;
            stepRoutine = null;
        }

        /// <summary>Releases the held box without changing the player's collider or visual transform.</summary>
        public void Release()
        {
            ResetBufferedInput();
            bool hasGrabbedBox = GrabbedBox != null;
            if (stepRoutine != null)
            {
                bool boxStepInProgress = hasGrabbedBox && GrabbedBox.IsMoving;
                StopCoroutine(stepRoutine);
                stepRoutine = null;
                if (boxStepInProgress && grid != null && body != null)
                {
                    body.position = grid.CellToWorld(stepTargetCell);
                }
            }

            if (hasGrabbedBox)
            {
                SetGrabbedBoxCollisionIgnored(false);
                GrabbedBox.IsGrabbed = false;
                GrabbedBox = null;
            }

            if (grid != null && body != null) playerCell = grid.WorldToCell(body.position);
            if (controller != null) controller.SetMovementEnabled(true);
        }

        private void SetGrabbedBoxCollisionIgnored(bool ignored)
        {
            if (playerCollider == null || GrabbedBox == null || GrabbedBox.Collider == null) return;
            Physics2D.IgnoreCollision(playerCollider, GrabbedBox.Collider, ignored);
        }

        private bool TryStep(Vector2Int direction)
        {
            bool isPush = direction == dragAxis;
            bool canStep = isPush
                ? GrabbedBox.CanMove(direction, playerCollider)
                : grid.IsCellFree(playerCell + direction, playerCollider, GrabbedBox.Collider);
            if (!canStep) return false;
            stepRoutine = StartCoroutine(StepRoutine(direction));
            return true;
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
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(Zero, One, progress);
                body.MovePosition(Vector2.Lerp(start, target, easedProgress));
            }
            body.position = target;
            playerCell = stepTargetCell;
            stepRoutine = null;
        }

        private void HandleTimelineChanged(TimelineEra era) { Release(); }
    }
}
