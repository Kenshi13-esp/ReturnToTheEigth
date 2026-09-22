using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Prototype HUD for the knight puzzle: controls, placement hints, failure notice and the win banner.</summary>
    public sealed class KnightPuzzleFeedback : MonoBehaviour
    {
        private const string Title = "MODO SELECCIÓN";
        private const string ControlsText = "WASD: mover el haz\nEspacio: colocar el caballo";
        private const string IdleText = "Lleva el haz a una casilla iluminada y pulsa Espacio";
        private const string PlacedText = "Caballo colocado...";
        private const string NotLitText = "Solo puedes colocarlo en una casilla iluminada";
        private const string FailedText = "Posición incorrecta...";
        private const string WinText = "YOU WIN";
        private const float RejectMessageSeconds = 1.5f;
        private const float PanelX = 8f;
        private const float PanelY = 8f;
        private const float PanelWidth = 240f;
        private const float PanelHeight = 130f;
        private const float Inset = 8f;
        private const float TitleHeight = 24f;
        private const float ControlsHeight = 40f;
        private const float MessageHeight = 50f;
        private const int FontSize = 12;
        private static readonly Color TitleColor = new Color(1f, 0.95f, 0.7f, 1f);
        private static readonly Color PlacedColor = new Color(0.6f, 1f, 0.8f, 1f);
        private static readonly Color RejectColor = new Color(1f, 0.8f, 0.4f, 1f);
        private static readonly Color FailedColor = new Color(1f, 0.45f, 0.4f, 1f);
        private static readonly Color WinColor = new Color(0.55f, 1f, 0.55f, 1f);

        [SerializeField] private KnightPlacementBoard board;
        [SerializeField] private KnightCursorController cursorController;
        private bool hasFailed;
        private GUIStyle labelStyle;

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
        }

        private void OnDisable()
        {
            if (board == null) return;
            board.Failed -= HandleFailed;
            board.Restarted -= HandleRestarted;
        }

        private void OnGUI()
        {
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = true };
            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUIContent.none);
            float x = PanelX + Inset;
            float y = PanelY + Inset;
            float width = PanelWidth - Inset - Inset;
            labelStyle.normal.textColor = TitleColor;
            GUI.Label(new Rect(x, y, width, TitleHeight), Title, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + TitleHeight, width, ControlsHeight), ControlsText, labelStyle);
            bool isSolved = board != null && board.IsSolved;
            bool isBusy = board != null && board.IsBusy;
            bool hasRecentReject = cursorController != null
                && Time.time - cursorController.LastRejectTime < RejectMessageSeconds;
            string message;
            Color messageColor;
            if (isSolved) { message = WinText; messageColor = WinColor; }
            else if (hasFailed) { message = FailedText; messageColor = FailedColor; }
            else if (isBusy) { message = PlacedText; messageColor = PlacedColor; }
            else if (hasRecentReject) { message = NotLitText; messageColor = RejectColor; }
            else { message = IdleText; messageColor = Color.white; }
            labelStyle.normal.textColor = messageColor;
            GUI.Label(new Rect(x, y + TitleHeight + ControlsHeight, width, MessageHeight), message, labelStyle);
        }

        private void HandleFailed() { hasFailed = true; }

        private void HandleRestarted() { hasFailed = false; }
    }
}
