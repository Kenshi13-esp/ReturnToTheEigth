using ReturnToTheEigth.Events;
using ReturnToTheEigth.UI;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Shows a banner once the box puzzle is solved.</summary>
    public sealed class PuzzleSolvedFeedback : MonoBehaviour
    {
        private const string SolvedText = "PUZZLE RESUELTO";

        private const float PanelWidthRatio = 0.7f;
        private const float PanelHeight = 108f;
        private const float PanelBottomInset = 104f;

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private PuzzleExitZone exitZone;
        private bool isSolved;

        private void Awake()
        {
            if (exitZone == null) exitZone = FindAnyObjectByType<PuzzleExitZone>();
        }

        private void Start()
        {
            isSolved = exitZone != null && exitZone.IsSolved;
        }

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
            if (!isSolved) return;

            float width = Screen.width * PanelWidthRatio;
            Rect rect = new Rect((Screen.width - width) * 0.5f,
                Screen.height - PanelHeight - PanelBottomInset, width, PanelHeight);
            GameTextGUI.DrawLabel(rect, SolvedText, TextAnchor.MiddleCenter);
        }

        private void HandlePuzzleSolved() { isSolved = true; }
    }
}
