using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Moves a board cursor with WASD and rotates the selected wire tile with the Interact action.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleCursorController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string InteractActionPath = "Player/Interact";
        private const string MissingInputError = "ElectricityPuzzleCursorController requires Player/Move and Player/Interact actions.";
        private const string MissingBoardWarning = "ElectricityPuzzleCursorController could not find an ElectricityPuzzleBoard.";
        private const float DefaultInputThreshold = 0.5f;
        private const float DefaultRepeatDelay = 0.3f;
        private const float DefaultRepeatInterval = 0.12f;
        private const float MinimumThreshold = 0.05f;
        private const float MaximumThreshold = 1f;
        private const float Zero = 0f;
        private static readonly Vector2Int ZeroCell = Vector2Int.zero;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private ElectricityPuzzleBoard board;
        [SerializeField] private Vector2Int startCell = ZeroCell;
        [SerializeField, Range(MinimumThreshold, MaximumThreshold)] private float inputThreshold = DefaultInputThreshold;
        [SerializeField, Min(Zero)] private float repeatDelay = DefaultRepeatDelay;
        [SerializeField, Min(Zero)] private float repeatInterval = DefaultRepeatInterval;

        private InputAction moveAction;
        private InputAction interactAction;
        private Vector2Int heldDirection;
        private float nextRepeatTime;

        /// <summary>Gets the cursor's current board coordinate.</summary>
        public Vector2Int CursorCell { get; private set; }

        private bool AllowsPuzzleInput => gameStateChannel == null
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
            if (board == null)
            {
                board = FindAnyObjectByType<ElectricityPuzzleBoard>();
            }
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
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            interactAction?.Dispose();
        }

        private void Start()
        {
            if (board == null || board.Grid == null)
            {
                return;
            }

            SetCursorCell(startCell);
            GameManager.Instance?.SetGameState(GameState.Puzzle);
        }

        private void Update()
        {
            if (!AllowsPuzzleInput || board == null || board.IsSolved)
            {
                return;
            }

            if (interactAction != null && interactAction.WasPerformedThisFrame())
            {
                board.TryRotateTile(CursorCell);
            }

            Vector2Int step = ReadStep();
            if (step != Vector2Int.zero)
            {
                MoveCursor(step);
            }
        }

        /// <summary>Moves the cursor to a valid board coordinate.</summary>
        public void SetCursorCell(Vector2Int cell)
        {
            if (board == null || board.Grid == null || !board.IsInside(cell))
            {
                return;
            }

            if (board.Grid.IsInside(CursorCell))
            {
                board.SetTileSelected(CursorCell, false);
            }

            CursorCell = cell;
            board.SetTileSelected(CursorCell, true);
        }

        private void MoveCursor(Vector2Int step)
        {
            SetCursorCell(CursorCell + step);
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
            if (input.magnitude < inputThreshold)
            {
                return Vector2Int.zero;
            }
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                return input.x > Zero ? Vector2Int.right : Vector2Int.left;
            }
            return input.y > Zero ? Vector2Int.up : Vector2Int.down;
        }

    }
}
