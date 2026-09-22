using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Prototype HUD for the color track puzzle: controls, current selection and the solved banner.</summary>
    public sealed class ColorPuzzleFeedback : MonoBehaviour
    {
        private const string Title = "MODO SELECCIÓN";
        private const string ControlsText = "WASD: mover el haz\nEspacio: seleccionar / soltar";
        private const string SelectedPrefix = "Olla seleccionada: ";
        private const string IdleText = "Sitúa el haz sobre una olla";
        private const string SolvedText = "PUZZLE RESUELTO";
        private const float PanelX = 8f;
        private const float PanelY = 8f;
        private const float PanelWidth = 190f;
        private const float PanelHeight = 118f;
        private const float Inset = 8f;
        private const float TitleHeight = 24f;
        private const float ControlsHeight = 40f;
        private const float MessageHeight = 40f;
        private const int FontSize = 12;
        private static readonly Color TitleColor = new Color(1f, 0.95f, 0.7f, 1f);
        private static readonly Color SolvedColor = new Color(0.55f, 1f, 0.55f, 1f);

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private TrackCursorController cursorController;
        private bool isSolved;
        private GUIStyle labelStyle;

        private void OnEnable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised -= HandlePuzzleSolved;
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
            TrackSlider selected = cursorController != null ? cursorController.SelectedSlider : null;
            string message = isSolved ? SolvedText : selected != null
                ? SelectedPrefix + PuzzleColorPalette.GetSpanishName(selected.ColorId) : IdleText;
            labelStyle.normal.textColor = isSolved ? SolvedColor : selected != null
                ? PuzzleColorPalette.GetColor(selected.ColorId) : Color.white;
            GUI.Label(new Rect(x, y + TitleHeight + ControlsHeight, width, MessageHeight), message, labelStyle);
        }

        private void HandlePuzzleSolved() { isSolved = true; }
    }
}
