using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Selection mode: moves the light beam with WASD, selects a piece with Interact and slides it along the tracks.</summary>
    [DisallowMultipleComponent]
    public sealed class TrackCursorController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string InteractActionPath = "Player/Interact";
        private const string MissingInputError = "TrackCursorController requires Player/Move and Player/Interact actions.";
        private const string MissingBoardWarning = "TrackCursorController could not find a TrackBoard; the cursor is disabled.";
        private const float DefaultInputThreshold = 0.5f;
        private const float DefaultRepeatDelay = 0.3f;
        private const float DefaultRepeatInterval = 0.12f;
        private const float MinimumThreshold = 0.05f;
        private const float MaximumThreshold = 1f;
        private const float Zero = 0f;
        private const int FirstIndex = 0;
        private const int LastOffset = 1;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private TrackBoard board;
        [SerializeField] private CursorBeamView beamView;
        [SerializeField] private Vector2Int startCell = Vector2Int.one;
        [SerializeField, Range(MinimumThreshold, MaximumThreshold)] private float inputThreshold = DefaultInputThreshold;
        [SerializeField, Min(Zero)] private float repeatDelay = DefaultRepeatDelay;
        [SerializeField, Min(Zero)] private float repeatInterval = DefaultRepeatInterval;
        private InputAction moveAction;
        private InputAction interactAction;
        private Vector2Int heldDirection;
        private float nextRepeatTime;

        public Vector2Int CursorCell { get; private set; }
        public TrackSlider SelectedSlider { get; private set; }
        public bool HasSelection => SelectedSlider != null;
        private PuzzleGrid Grid => board != null ? board.Grid : null;
        private bool AllowsGameplay => gameStateChannel == null
            || gameStateChannel.CurrentState == GameState.Exploration
            || gameStateChannel.CurrentState == GameState.Puzzle;

        private void Awake()
        {
            InputAction sourceMove = inputActions != null ? inputActions.FindAction(MoveActionPath) : null;
            InputAction sourceInteract = inputActions != null ? inputActions.FindAction(InteractActionPath) : null;
            if (sourceMove == null || sourceInteract == null)
            {
                Debug.LogError(MissingInputError, this);
                enabled = false;
                return;
            }
            moveAction = sourceMove.Clone();
            interactAction = sourceInteract.Clone();
            if (board == null) board = FindAnyObjectByType<TrackBoard>();
            if (beamView == null) beamView = GetComponentInChildren<CursorBeamView>();
            if (board == null)
            {
                Debug.LogWarning(MissingBoardWarning, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            interactAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            interactAction?.Disable();
            Deselect();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            interactAction?.Dispose();
        }

        private void Start()
        {
            if (Grid != null)
            {
                Vector2Int size = Grid.Size;
                CursorCell = new Vector2Int(
                    Mathf.Clamp(startCell.x, FirstIndex, size.x - LastOffset),
                    Mathf.Clamp(startCell.y, FirstIndex, size.y - LastOffset));
                beamView?.MoveTo(Grid.CellToWorld(CursorCell));
            }
            if (GameManager.Instance != null) GameManager.Instance.SetGameState(GameState.Puzzle);
        }

        private void Update()
        {
            if (!AllowsGameplay || Grid == null || board.IsSolved) return;
            if (interactAction != null && interactAction.WasPerformedThisFrame()) ToggleSelection();
            Vector2Int step = ReadStep();
            if (step == Vector2Int.zero) return;
            if (HasSelection) TrySlide(step);
            else MoveCursor(step);
        }

        /// <summary>Drops the selected piece (if any) and returns the beam to the cursor cell.</summary>
        public void Deselect()
        {
            if (SelectedSlider != null)
            {
                SelectedSlider.IsSelected = false;
                SelectedSlider = null;
            }
            beamView?.SetSelected(false);
            if (beamView != null && Grid != null) beamView.MoveTo(Grid.CellToWorld(CursorCell));
        }

        private void ToggleSelection()
        {
            if (HasSelection)
            {
                Deselect();
                return;
            }
            TrackSlider candidate = board.GetSliderAt(CursorCell);
            if (candidate == null || candidate.IsMoving) return;
            SelectedSlider = candidate;
            candidate.IsSelected = true;
            beamView?.SetSelected(true);
        }

        private void MoveCursor(Vector2Int step)
        {
            Vector2Int next = CursorCell + step;
            if (!Grid.IsInside(next)) return;
            CursorCell = next;
            beamView?.MoveTo(Grid.CellToWorld(CursorCell));
        }

        private void TrySlide(Vector2Int step)
        {
            if (SelectedSlider.IsMoving) return;
            Vector2Int destination = board.ComputeSlideDestination(SelectedSlider, step);
            if (destination == SelectedSlider.Cell) return;
            SelectedSlider.SlideTo(destination);
            CursorCell = destination;
            beamView?.FollowTarget(SelectedSlider.transform);
        }

        private Vector2Int ReadStep()
        {
            Vector2Int direction = ToCardinal(moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero);
            if (direction != heldDirection)
            {
                heldDirection = direction;
                nextRepeatTime = Time.time + repeatDelay;
                return direction;
            }
            if (direction != Vector2Int.zero && Time.time >= nextRepeatTime)
            {
                nextRepeatTime = Time.time + repeatInterval;
                return direction;
            }
            return Vector2Int.zero;
        }

        private Vector2Int ToCardinal(Vector2 input)
        {
            if (input.magnitude < inputThreshold) return Vector2Int.zero;
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y)) return input.x > Zero ? Vector2Int.right : Vector2Int.left;
            return input.y > Zero ? Vector2Int.up : Vector2Int.down;
        }
    }
}
