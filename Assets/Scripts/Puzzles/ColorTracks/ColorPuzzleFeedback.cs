using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shows the current color-track selection and the solved banner.</summary>
    public sealed class ColorPuzzleFeedback : MonoBehaviour
    {
        private const string SelectedPrefix = "Olla seleccionada: ";
        private const string SolvedText = "PUZZLE RESUELTO";
        private const string HallSceneName = "Hall";
        private const float PanelWidthRatio = 0.84f;
        private const float PanelBottomInset = 24f;
        private const float MessageHeight = 112f;
        private const float RowGap = 8f;

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private TrackCursorController cursorController;
        private bool isSolved;

        private void OnEnable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised -= HandlePuzzleSolved;
        }

        private void Start()
        {
            TrackBoard board = FindAnyObjectByType<TrackBoard>();
            if (board != null && board.IsSolved)
            {
                HandlePuzzleSolved();
            }
        }

        private void OnGUI()
        {
            float panelWidth = Screen.width * PanelWidthRatio;
            float x = (Screen.width - panelWidth) * 0.5f;
            float totalHeight = MessageHeight + RowGap;
            float y = Screen.height - totalHeight - PanelBottomInset;
            TrackSlider selected = cursorController != null ? cursorController.SelectedSlider : null;
            string message = isSolved ? SolvedText
                : selected != null ? SelectedPrefix + PuzzleColorPalette.GetSpanishName(selected.ColorId) : string.Empty;

            GameTextGUI.DrawLabel(new Rect(x, y, panelWidth, MessageHeight), message, TextAnchor.MiddleCenter);
        }

        private void HandlePuzzleSolved()
        {
            if (isSolved)
            {
                return;
            }

            isSolved = true;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Exploration);
            }

            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }
    }
}
