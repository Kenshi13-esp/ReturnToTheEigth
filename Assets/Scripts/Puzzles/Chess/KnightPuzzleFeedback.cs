using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Returns to Hall when the knight placement puzzle is solved.</summary>
    public sealed class KnightPuzzleFeedback : MonoBehaviour
    {
        private const string HallSceneName = "Hall";

        [SerializeField] private KnightPlacementBoard board;
        private bool isReturningToHall;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<KnightPlacementBoard>();
        }

        private void OnEnable()
        {
            if (board != null) board.Solved += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (board != null) board.Solved -= HandlePuzzleSolved;
        }

        private void Start()
        {
            if (board != null && board.IsSolved) HandlePuzzleSolved();
        }

        private void HandlePuzzleSolved()
        {
            if (isReturningToHall) return;

            isReturningToHall = true;
            GameManager.Instance?.SetGameState(GameState.Exploration);
            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }
    }
}
