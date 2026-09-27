using ReturnToTheEigth.Core;
using ReturnToTheEigth.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Shows electricity puzzle status and returns to Hall when the complete circuit is formed.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleFeedback : MonoBehaviour
    {
        private const string PuzzleTitle = "ELECTRICIDAD";

        private const string SolvedText = "CORRIENTE RESTABLECIDA";
        private const string HallSceneName = "Hall";
        private const float PanelWidthRatio = 0.84f;
        private const float PanelBottomInset = 24f;
        private const float TitleHeight = 64f;
        private const float StatusHeight = 100f;
        private const float RowGap = 8f;
        

        [SerializeField] private ElectricityPuzzleBoard board;
        [SerializeField] private ElectricityPuzzleCursorController cursorController;

        private bool isReturningToHall;

        private void Awake()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<ElectricityPuzzleBoard>();
            }
            if (cursorController == null)
            {
                cursorController = FindAnyObjectByType<ElectricityPuzzleCursorController>();
            }
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.Solved += HandlePuzzleSolved;
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.Solved -= HandlePuzzleSolved;
            }
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
            float totalHeight = TitleHeight + StatusHeight + RowGap;
            float y = Screen.height - totalHeight - PanelBottomInset;
            bool solved = board != null && board.IsSolved;
            string status = solved ? SolvedText : string.Empty;
            if (!solved && board != null)
            {
                status = string.Format("Recuadros iluminados: {0}/{1}", board.PoweredTileCount, board.TileCount);
            }

            GameTextGUI.DrawLabel(new Rect(x, y, panelWidth, TitleHeight), PuzzleTitle, TextAnchor.MiddleCenter);
            y += TitleHeight + RowGap;
            GameTextGUI.DrawLabel(new Rect(x, y, panelWidth, StatusHeight), status, TextAnchor.MiddleCenter);

            float markerSize = 64f;
            float boardLabelY = Screen.height * 0.7f;
            GameTextGUI.DrawLabel(new Rect(Screen.width * 0.29f, boardLabelY, markerSize, markerSize), "A");
            GameTextGUI.DrawLabel(new Rect(Screen.width * 0.71f, Screen.height * 0.22f, markerSize, markerSize), "B");
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

    }
}
