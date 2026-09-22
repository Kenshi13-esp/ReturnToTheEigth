using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Selection mode for the knight puzzle: WASD moves the light beam over any cell, Interact places the knight on the lit cell under the beam.</summary>
    [DisallowMultipleComponent]
    public sealed class KnightCursorController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string InteractActionPath = "Player/Interact";
        private const string MissingInputError = "KnightCursorController requires Player/Move and Player/Interact actions.";
        private const string MissingBoardWarning = "KnightCursorController could not find a KnightPlacementBoard; the cursor is disabled.";
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
        [SerializeField] private KnightPlacementBoard board;
        [SerializeField] private CursorBeamView beamView;
        [SerializeField] private Vector2Int startCell = new Vector2Int(2, 2);
        [SerializeField, Range(MinimumThreshold, MaximumThreshold)] private float inputThreshold = DefaultInputThreshold;
        [SerializeField, Min(Zero)] private float repeatDelay = DefaultRepeatDelay;
        [SerializeField, Min(Zero)] private float repeatInterval = DefaultRepeatInterval;
        private InputAction moveAction;
        private InputAction interactAction;
        private Vector2Int heldDirection;
        private float nextRepeatTime;

        public Vector2Int CursorCell { get; private set; }
        /// <summary>Time.time of the last Interact press on a cell that is not lit (negative infinity when none).</summary>
        public float LastRejectTime { get; private set; } = float.NegativeInfinity;
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
            if (board == null) board = FindAnyObjectByType<KnightPlacementBoard>();
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
            if (board != null)
            {
                board.Placed += HandlePlaced;
                board.Restarted += HandleRestarted;
            }
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            interactAction?.Disable();
            if (board != null)
            {
                board.Placed -= HandlePlaced;
                board.Restarted -= HandleRestarted;
            }
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            interactAction?.Dispose();
        }

        private void Start()
        {
            PlaceCursor(startCell);
            if (GameManager.Instance != null) GameManager.Instance.SetGameState(GameState.Puzzle);
        }

        private void Update()
        {
            if (!AllowsGameplay || Grid == null || board.IsBusy || board.IsSolved) return;
            if (interactAction != null && interactAction.WasPerformedThisFrame()) HandleInteract();
            Vector2Int step = ReadStep();
            if (step != Vector2Int.zero) MoveCursor(step);
        }

        private void HandleInteract()
        {
            if (!board.TryPlaceKnight(CursorCell)) LastRejectTime = Time.time;
        }

        private void MoveCursor(Vector2Int step)
        {
            Vector2Int next = CursorCell + step;
            if (!Grid.IsInside(next)) return;
            CursorCell = next;
            beamView?.MoveTo(Grid.CellToWorld(CursorCell));
        }

        private void PlaceCursor(Vector2Int cell)
        {
            if (Grid == null) return;
            Vector2Int size = Grid.Size;
            CursorCell = new Vector2Int(
                Mathf.Clamp(cell.x, FirstIndex, size.x - LastOffset),
                Mathf.Clamp(cell.y, FirstIndex, size.y - LastOffset));
            beamView?.MoveTo(Grid.CellToWorld(CursorCell));
        }

        private void HandlePlaced()
        {
            beamView?.SetSelected(true);
        }

        private void HandleRestarted()
        {
            beamView?.SetSelected(false);
            PlaceCursor(startCell);
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
