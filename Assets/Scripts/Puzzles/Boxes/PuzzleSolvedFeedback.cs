using ReturnToTheEigth.Events;
using ReturnToTheEigth.Player;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Prototype HUD showing the drag hint while a box is held and a banner once the puzzle is solved.</summary>
    public sealed class PuzzleSolvedFeedback : MonoBehaviour
    {
        private const string SolvedText = "PUZZLE RESUELTO";
        private const string DraggingText = "Arrastrando caja: W/S o A/D\nEspacio: soltar";
        private const string HintText = "Mira una caja y pulsa Espacio\npara agarrarla.";
        private const float PanelX = 190f;
        private const float PanelY = 8f;
        private const float PanelWidth = 174f;
        private const float PanelHeight = 64f;
        private const float Inset = 8f;
        private const int FontSize = 12;
        private static readonly Color SolvedColor = new Color(0.55f, 1f, 0.55f, 1f);
        private static readonly Color DraggingColor = new Color(1f, 0.85f, 0.45f, 1f);

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private BoxDragController dragController;
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
            bool isDragging = dragController != null && dragController.IsGrabbing;
            string message = isSolved ? SolvedText : isDragging ? DraggingText : HintText;
            labelStyle.normal.textColor = isSolved ? SolvedColor : isDragging ? DraggingColor : Color.white;
            GUI.Label(new Rect(PanelX + Inset, PanelY + Inset, PanelWidth - Inset - Inset, PanelHeight - Inset - Inset), message, labelStyle);
        }

        private void HandlePuzzleSolved() { isSolved = true; }
    }
}
