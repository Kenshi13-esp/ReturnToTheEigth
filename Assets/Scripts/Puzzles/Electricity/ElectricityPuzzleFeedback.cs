using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Shows electricity puzzle controls and returns to Hall when the complete circuit is formed.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleFeedback : MonoBehaviour
    {
        private const string PuzzleTitle = "ELECTRICIDAD";
        private const string ControlsText = "WASD: mover selector\nE: girar pieza 90°";
        private const string WaitingText = "Lleva la corriente desde A hasta B";
        private const string SolvedText = "CORRIENTE RESTABLECIDA";
        private const string HallSceneName = "Hall";
        private const float PanelX = 14f;
        private const float PanelY = 14f;
        private const float PanelWidth = 240f;
        private const float PanelHeight = 112f;
        private const float Inset = 10f;
        private const int TitleFontSize = 16;
        private const int BodyFontSize = 13;
        private static readonly Color TitleColor = new Color(1f, 0.9f, 0.45f, 1f);
        private static readonly Color PoweredColor = new Color(1f, 0.75f, 0.15f, 1f);
        private static readonly Color InputColor = new Color(0.2f, 0.95f, 0.25f, 1f);
        private static readonly Color OutputColor = new Color(1f, 0.16f, 0.12f, 1f);

        [SerializeField] private ElectricityPuzzleBoard board;
        [SerializeField] private ElectricityPuzzleCursorController cursorController;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
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
            EnsureStyles();
            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUIContent.none);
            float textX = PanelX + Inset;
            float textWidth = PanelWidth - Inset * 2f;
            titleStyle.normal.textColor = TitleColor;
            GUI.Label(new Rect(textX, PanelY + Inset, textWidth, 24f), PuzzleTitle, titleStyle);
            bodyStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(textX, PanelY + Inset + 28f, textWidth, 38f), ControlsText, bodyStyle);

            bool solved = board != null && board.IsSolved;
            string status = solved ? SolvedText : WaitingText;
            if (!solved && board != null)
            {
                status = string.Format("Recuadros iluminados: {0}/{1}", board.PoweredTileCount, board.TileCount);
            }
            bodyStyle.normal.textColor = solved ? PoweredColor : Color.white;
            GUI.Label(new Rect(textX, PanelY + Inset + 69f, textWidth, 30f), status, bodyStyle);

            float boardLabelY = Screen.height * 0.7f;
            GUI.color = InputColor;
            GUI.Label(new Rect(Screen.width * 0.29f, boardLabelY, 48f, 32f), "A", bodyStyle);
            GUI.color = OutputColor;
            GUI.Label(new Rect(Screen.width * 0.71f, Screen.height * 0.22f, 48f, 32f), "B", bodyStyle);
            GUI.color = Color.white;
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

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = TitleFontSize,
                    fontStyle = FontStyle.Bold
                };
            }
            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = BodyFontSize,
                    wordWrap = true
                };
            }
        }
    }
}
