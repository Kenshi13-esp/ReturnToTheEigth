using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReturnToTheEigth.UI;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shows knight placement status, failure feedback and the win banner.</summary>
    public sealed class KnightPuzzleFeedback : MonoBehaviour
    {
        private const string Title = "MODO SELECCIÓN";
        private const string PlacedText = "Caballo colocado...";
        private const string NotLitText = "Solo puedes colocarlo en una casilla iluminada";
        private const string FailedText = "Posición incorrecta...";
        private const string WinText = "YOU WIN";
        private const string HallSceneName = "Hall";
        private const float RejectMessageSeconds = 1.5f;
        private const float PanelWidthRatio = 0.84f;
        private const float PanelBottomInset = 24f;
        private const float TitleHeight = 64f;
        private const float MessageHeight = 132f;
        private const float RowGap = 8f;

        [SerializeField] private KnightPlacementBoard board;
        [SerializeField] private KnightCursorController cursorController;
        private bool hasFailed;
        private bool isReturningToHall;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<KnightPlacementBoard>();
            if (cursorController == null) cursorController = FindAnyObjectByType<KnightCursorController>();
        }

        private void OnEnable()
        {
            if (board == null) return;
            board.Failed += HandleFailed;
            board.Restarted += HandleRestarted;
            board.Solved += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (board == null) return;
            board.Failed -= HandleFailed;
            board.Restarted -= HandleRestarted;
            board.Solved -= HandlePuzzleSolved;
        }

        private void Start()
        {
            if (board != null && board.IsSolved)
            {
                HandlePuzzleSolved();
            }
        }

        private void OnGUI()
        {
            float panelWidth = Screen.width * PanelWidthRatio;
            float x = (Screen.width - panelWidth) * 0.5f;
            float totalHeight = TitleHeight + MessageHeight + RowGap;
            float y = Screen.height - totalHeight - PanelBottomInset;
            bool isSolved = board != null && board.IsSolved;
            bool isBusy = board != null && board.IsBusy;
            bool hasRecentReject = cursorController != null
                && Time.time - cursorController.LastRejectTime < RejectMessageSeconds;
            string message;
            if (isSolved) message = WinText;
            else if (hasFailed) message = FailedText;
            else if (isBusy) message = PlacedText;
            else if (hasRecentReject) message = NotLitText;
            else message = string.Empty;

            GameTextGUI.DrawLabel(new Rect(x, y, panelWidth, TitleHeight), Title, TextAnchor.MiddleCenter);
            y += TitleHeight + RowGap;
            GameTextGUI.DrawLabel(new Rect(x, y, panelWidth, MessageHeight), message, TextAnchor.MiddleCenter);
        }

        private void HandlePuzzleSolved()
        {
            if (isReturningToHall)
            {
                return;
            }

            isReturningToHall = true;
            GameManager.Instance?.SetGameState(GameState.Exploration);
            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }

        private void HandleFailed() { hasFailed = true; }

        private void HandleRestarted() { hasFailed = false; }
    }
}
